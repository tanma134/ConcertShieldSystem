using Microsoft.EntityFrameworkCore;
using TicketAPI.Domain;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;

namespace TicketAPI.Services
{
    // UC_9: submit, track and cancel ticket return requests.
    public class TicketReturnService : ITicketReturnService
    {
        private readonly TicketDbContext _context;
        private readonly ILogger<TicketReturnService> _logger;
        private readonly IEventApiClient _eventApiClient;
        private readonly IConfiguration _configuration;
        private readonly IPaymentAPIClient _paymentClient;
        private readonly TicketReturnNotifier _notifier;

        public TicketReturnService(TicketDbContext context, ILogger<TicketReturnService> logger, IEventApiClient eventApiClient, IConfiguration configuration,
            IPaymentAPIClient paymentClient, TicketReturnNotifier notifier)
        {
            _context = context;
            _logger = logger;
            _eventApiClient = eventApiClient;
            _configuration = configuration;
            _paymentClient = paymentClient;
            _notifier = notifier;
        }

        // Every paid ticket the user owns, with whether it can be returned right now.
        public async Task<List<MyTicketDto>> ListMyTicketsAsync(int userId)
        {
            var now = DateTime.UtcNow;

            var tickets = await _context.Tickets
                .AsNoTracking()
                .Include(t => t.Order).ThenInclude(o => o.OrderDetails)
                .Where(t => t.OwnerUserId == userId && !t.IsDeleted && t.Order.Status == "Paid")
                .ToListAsync();

            // Do not touch the return-request table when this account owns no paid tickets.
            // This also makes Staff/Admin accounts correctly receive [] instead of failing.
            if (tickets.Count == 0)
                return new List<MyTicketDto>();

            var ticketIds = tickets.Select(t => t.TicketId).ToList();

            // One call per event: the organizer's refund tiers decide the window and the percentage.
            var tiersByEvent = new Dictionary<int, IReadOnlyList<RefundTier>>();
            foreach (var eventId in tickets.Select(t => t.EventId).Distinct())
                tiersByEvent[eventId] = await _eventApiClient.GetRefundTiersAsync(eventId);

            var requests = await _context.TicketReturnRequests
                .AsNoTracking()
                .Where(r => ticketIds.Contains(r.TicketId))
                .ToListAsync();

            var changes = await (from affected in _context.Set<AffectedTicket>().AsNoTracking()
                join change in _context.Set<AppliedEventChange>().AsNoTracking() on affected.ChangeId equals change.ChangeId
                where ticketIds.Contains(affected.TicketId)
                select new { affected.TicketId, Change = change }).ToListAsync();
            var latestChanges = changes.GroupBy(x => x.TicketId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Change.ScheduleVersion).First().Change);

            return tickets
                .OrderByDescending(t => t.Order.StartsAt)
                .ThenBy(t => t.TicketId)
                .Select(ticket =>
                {
                    var latest = requests
                        .Where(r => r.TicketId == ticket.TicketId)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefault();

                    var hasOpen = latest != null && ReturnRequestStatuses.IsOpen(latest.Status);
                    var eligibility = EvaluatePolicy(ticket, hasOpen, now, tiersByEvent[ticket.EventId], latestChanges.GetValueOrDefault(ticket.TicketId));
                    var percent = ResolvePercent(eligibility, tiersByEvent[ticket.EventId]);

                    return new MyTicketDto
                    {
                        TicketId = ticket.TicketId,
                        TicketCode = ticket.TicketCode,
                        OrderId = ticket.OrderId,
                        EventId = ticket.EventId,
                        EventName = ticket.Order.EventName,
                        PosterUrl = ticket.Order.PosterUrl,
                        StartsAt = ticket.Order.StartsAt,
                        TicketTypeName = ticket.TicketTypeName,
                        SeatId = ticket.SeatId,
                        Status = ticket.Status ?? TicketStatuses.Active,
                        CheckedInAt = ticket.CheckedInAt,
                        CanReturn = eligibility.IsEligible,
                        ReturnCode = eligibility.Code,
                        ReturnMessage = eligibility.Message,
                        RefundEstimate = ComputeRefund(ticket, percent),
                        RefundPercent = percent,
                        LatestReturn = latest == null ? null : MapRequest(latest, ticket)
                    };
                })
                .ToList();
        }

        // UC_9.1: validates the ticket and the reason, then opens a Pending request
        // and moves the ticket to ReturnPending so its QR stops working.
        public async Task<TicketReturnDto> SubmitAsync(int userId, SubmitTicketReturnDto dto)
        {
            var reason = TicketReturnPolicy.ValidateReason(dto.Reason);
            if (!reason.IsValid)
                throw new ArgumentException(reason.Error);

            var ticket = await _context.Tickets
                .Include(t => t.Order).ThenInclude(o => o.OrderDetails)
                .Include(t => t.TicketQrTokens)
                .FirstOrDefaultAsync(t => t.TicketId == dto.TicketId && t.OwnerUserId == userId && !t.IsDeleted)
                ?? throw new KeyNotFoundException("Ticket not found.");

            var hasOpen = await _context.TicketReturnRequests
                .AnyAsync(r => r.TicketId == ticket.TicketId && r.Status == ReturnRequestStatuses.Pending);

            var now = DateTime.UtcNow;
            var tiers = await _eventApiClient.GetRefundTiersAsync(ticket.EventId);
            var change = await (from affected in _context.Set<AffectedTicket>().AsNoTracking()
                join applied in _context.Set<AppliedEventChange>().AsNoTracking() on affected.ChangeId equals applied.ChangeId
                where affected.TicketId == ticket.TicketId
                orderby applied.ScheduleVersion descending select applied).FirstOrDefaultAsync();
            var eligibility = EvaluatePolicy(ticket, hasOpen, now, tiers, change);
            if (!eligibility.IsEligible)
                throw new InvalidOperationException(eligibility.Message);

            var request = new TicketReturnRequest
            {
                TicketId = ticket.TicketId,
                OrderId = ticket.OrderId,
                EventId = ticket.EventId,
                RequesterUserId = userId,
                Reason = reason.Reason,
                Status = ReturnRequestStatuses.Pending,
                // The percentage is frozen at submission, so a later policy change cannot alter this request.
                RefundAmount = ComputeRefund(ticket, eligibility.RefundPercent),
                CreatedAt = now,
                UpdatedAt = now
            };

            ticket.Status = TicketStatuses.ReturnPending;
            RevokeQrTokens(ticket, now);
            _context.TicketReturnRequests.Add(request);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // The unique index allows one open request per ticket, so a double click lands here.
                _logger.LogWarning(ex, "Return request for ticket {TicketId} was rejected by the database.", ticket.TicketId);
                throw new InvalidOperationException("A return request for this ticket is already waiting for review.");
            }

            return MapRequest(request, ticket);
        }

        // UC_9.2: the user's requests, newest first, optionally filtered by status.
        public async Task<List<TicketReturnDto>> ListMineAsync(int userId, string? status)
        {
            var query = _context.TicketReturnRequests
                .AsNoTracking()
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .Where(r => r.RequesterUserId == userId);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            return requests.Select(r => MapRequest(r, r.Ticket)).ToList();
        }

        // UC_9.2: one request. Other people's requests look like "not found".
        public async Task<TicketReturnDto> GetMineAsync(int userId, int requestId)
        {
            var request = await _context.TicketReturnRequests
                .AsNoTracking()
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .FirstOrDefaultAsync(r => r.TicketReturnRequestId == requestId && r.RequesterUserId == userId)
                ?? throw new KeyNotFoundException("Return request not found.");

            return MapRequest(request, request.Ticket);
        }

        // UC_9.3: withdraws a Pending request and gives the ticket back to its owner.
        public async Task<TicketReturnDto> CancelAsync(int userId, int requestId)
        {
            var request = await _context.TicketReturnRequests
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .FirstOrDefaultAsync(r => r.TicketReturnRequestId == requestId && r.RequesterUserId == userId)
                ?? throw new KeyNotFoundException("Return request not found.");

            if (!TicketReturnPolicy.CanCancel(request.Status))
                throw new InvalidOperationException("Only a request that is still waiting for review can be cancelled.");

            var now = DateTime.UtcNow;
            request.Status = ReturnRequestStatuses.Cancelled;
            request.CancelledAt = now;
            request.UpdatedAt = now;

            if (request.Ticket.Status == TicketStatuses.ReturnPending)
                request.Ticket.Status = TicketStatuses.Active;

            await _context.SaveChangesAsync();

            return MapRequest(request, request.Ticket);
        }

        // Staff/Admin queue of return requests. eventScope limits Staff to the events they were assigned.
        public async Task<List<TicketReturnDto>> ListForReviewAsync(string? status, IReadOnlyCollection<int>? eventScope = null)
        {
            var query = _context.TicketReturnRequests
                .AsNoTracking()
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .AsQueryable();

            if (eventScope != null)
            {
                var ids = eventScope.ToList();
                query = query.Where(r => ids.Contains(r.EventId));
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            return requests.Select(r => MapRequest(r, r.Ticket)).ToList();
        }

        // Staff/Admin approves or rejects a Pending request. Approving also pays the money back.
        public async Task<TicketReturnDto> ReviewAsync(int reviewerUserId, int requestId, ReviewTicketReturnDto dto, IReadOnlyCollection<int>? eventScope = null)
        {
            var request = await LoadForReviewAsync(requestId, eventScope);

            if (request.Status != ReturnRequestStatuses.Pending)
                throw new InvalidOperationException("Only a pending return request can be reviewed.");

            var now = DateTime.UtcNow;
            request.Status = dto.Approve ? ReturnRequestStatuses.Approved : ReturnRequestStatuses.Rejected;
            request.ReviewedBy = reviewerUserId;
            request.ReviewedAt = now;
            request.UpdatedAt = now;
            request.ReviewNote = string.IsNullOrWhiteSpace(dto.ReviewNote) ? null : dto.ReviewNote.Trim();

            if (dto.Approve)
            {
                // Release inventory first. If EventAPI cannot do it, keep the request Pending
                // instead of creating a Returned ticket while inventory is still sold.
                var release = await _eventApiClient.TryReleaseReturnedTicketAsync(
                    request.EventId, request.Ticket.TicketTypeId, 1);
                if (release.NotFound)
                {
                    // The ticket type was removed from the event, so there is no inventory to give back.
                    // Do not block the customer's return because of that.
                    _logger.LogWarning("Return request {Id}: {Reason} Continuing without releasing inventory.",
                        request.TicketReturnRequestId, release.Error);
                }
                else if (!release.Success)
                {
                    throw new InvalidOperationException(
                        "Could not release the returned ticket back to event inventory. " + release.Error);
                }

                request.Ticket.Status = TicketStatuses.Returned;
                RevokeQrTokens(request.Ticket, now);
            }
            else if (request.Ticket.Status == TicketStatuses.ReturnPending)
            {
                request.Ticket.Status = TicketStatuses.Active;
            }

            // Save the decision before touching money, so a payment problem can never undo it.
            await _context.SaveChangesAsync();

            if (!dto.Approve)
            {
                await _notifier.NotifyCustomerAsync(request.RequesterUserId, request.TicketReturnRequestId, "rejected",
                    "Return request rejected",
                    $"Your return request for {request.Ticket.Order?.EventName ?? "your ticket"} was rejected." +
                    (request.ReviewNote == null ? "" : " Reason: " + request.ReviewNote));
                return MapRequest(request, request.Ticket);
            }

            await PayBackAsync(request);
            return MapRequest(request, request.Ticket);
        }

        // Staff/Admin retries the payout of an approved return whose refund failed.
        public async Task<TicketReturnDto> RetryRefundAsync(int reviewerUserId, int requestId, IReadOnlyCollection<int>? eventScope = null)
        {
            var request = await LoadForReviewAsync(requestId, eventScope);

            if (request.Status != ReturnRequestStatuses.RefundFailed && request.Status != ReturnRequestStatuses.Approved)
                throw new InvalidOperationException("Only an approved return whose refund has not been paid can be retried.");

            request.ReviewedBy ??= reviewerUserId;
            await PayBackAsync(request);
            return MapRequest(request, request.Ticket);
        }

        // Asks PaymentAPI to pay the refund and stores the outcome (Refunded or RefundFailed).
        private async Task PayBackAsync(TicketReturnRequest request)
        {
            var outcome = await _paymentClient.RefundAsync(request.TicketReturnRequestId, request.OrderId, request.RefundAmount);
            var now = DateTime.UtcNow;
            request.UpdatedAt = now;

            if (outcome.Success)
            {
                request.Status = ReturnRequestStatuses.Refunded;
                request.RefundedAt = now;
                request.RefundReference = outcome.Reference;
                request.RefundError = null;
            }
            else
            {
                request.Status = ReturnRequestStatuses.RefundFailed;
                var error = outcome.Error ?? "Refund was not completed.";
                request.RefundError = error.Length > 500 ? error[..500] : error;
            }

            await _context.SaveChangesAsync();

            var eventName = request.Ticket.Order?.EventName ?? "your ticket";
            var amount = request.RefundAmount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            if (outcome.Success)
                await _notifier.NotifyCustomerAsync(request.RequesterUserId, request.TicketReturnRequestId, "refunded",
                    "Return approved and refunded",
                    $"Your return for {eventName} was approved. {amount} VND is being paid back to you.");
            else
                await _notifier.NotifyCustomerAsync(request.RequesterUserId, request.TicketReturnRequestId, "approved",
                    "Return approved",
                    $"Your return for {eventName} was approved. We are processing your refund of {amount} VND and will update you.");
        }

        // Loads a request for review. A request outside the reviewer's events looks like "not found".
        private async Task<TicketReturnRequest> LoadForReviewAsync(int requestId, IReadOnlyCollection<int>? eventScope)
        {
            var request = await _context.TicketReturnRequests
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .FirstOrDefaultAsync(r => r.TicketReturnRequestId == requestId)
                ?? throw new KeyNotFoundException("Return request not found.");

            if (eventScope != null && !eventScope.Contains(request.EventId))
                throw new KeyNotFoundException("Return request not found.");

            return request;
        }

        // Chỉ vé thực sự thuộc snapshot mới nhận chính sách đổi lịch; không mở quyền cho toàn bộ event.
        private ReturnEligibility EvaluatePolicy(Ticket ticket, bool hasOpen, DateTime now, IReadOnlyList<RefundTier> tiers, AppliedEventChange? change)
        {
            if (change != null)
                return EventChangeReturnPolicy.Evaluate(ticket.Status, ticket.Order.Status, ticket.CheckedInAt, hasOpen, now, change.AppliedAt,
                    Math.Clamp(_configuration.GetValue("Governance:ChangeReturnWindowHours", 168), 1, 720),
                    _configuration.GetValue<decimal>("Governance:ChangeRefundPercent", 100));
            if (!ticket.Order.StartsAt.HasValue) return new(false, "SCHEDULE_UNAVAILABLE", "The current schedule cannot be verified.", 0);
            return TicketReturnPolicy.Evaluate(BuildFacts(ticket, hasOpen), now, tiers);
        }

        // Collects the facts the policy needs from a ticket and its order.
        private static TicketReturnFacts BuildFacts(Ticket ticket, bool hasOpenRequest)
        {
            return new TicketReturnFacts(
                ticket.Status,
                ticket.Order.Status,
                ticket.CheckedInAt,
                ToUtc(ticket.Order.StartsAt!.Value),
                hasOpenRequest);
        }

        // Event times arrive without a kind from the database; treat them as UTC.
        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        // Percentage shown to the customer: the applicable tier while returnable,
        // otherwise what the best tier would pay (so the card still shows the policy).
        private static decimal ResolvePercent(ReturnEligibility eligibility, IReadOnlyList<RefundTier> tiers)
        {
            if (eligibility.IsEligible)
                return eligibility.RefundPercent;

            return tiers.Count > 0 ? tiers.Max(t => t.RefundPercent) : TicketReturnPolicy.DefaultTier.RefundPercent;
        }

        // Refund for one ticket: its unit price minus its share of the order discount,
        // then the organizer's refund percentage.
        private static long ComputeRefund(Ticket ticket, decimal refundPercent)
        {
            var order = ticket.Order;
            var detail = order.OrderDetails.FirstOrDefault(d => d.TicketTypeId == ticket.TicketTypeId);
            var gross = order.OrderDetails.Sum(d => d.UnitPrice * d.Quantity);

            var paid = TicketReturnPolicy.RefundAmount(detail?.UnitPrice ?? 0, gross, order.DiscountAmount);
            return TicketReturnPolicy.ApplyPercent(paid, refundPercent);
        }

        // A ticket waiting for a return decision must not be scannable.
        private static void RevokeQrTokens(Ticket ticket, DateTime now)
        {
            foreach (var token in ticket.TicketQrTokens.Where(t => !t.IsRevoked))
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
            }
        }

        // Converts a request and its ticket into the DTO shown to the customer.
        private static TicketReturnDto MapRequest(TicketReturnRequest request, Ticket ticket)
        {
            return new TicketReturnDto
            {
                TicketReturnRequestId = request.TicketReturnRequestId,
                TicketId = request.TicketId,
                OrderId = request.OrderId,
                EventId = request.EventId,
                RequesterUserId = request.RequesterUserId,
                EventName = ticket.Order?.EventName,
                TicketTypeName = ticket.TicketTypeName,
                Reason = request.Reason,
                Status = request.Status,
                RefundAmount = request.RefundAmount,
                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt,
                CancelledAt = request.CancelledAt,
                ReviewedAt = request.ReviewedAt,
                ReviewedBy = request.ReviewedBy,
                ReviewNote = request.ReviewNote,
                CanCancel = TicketReturnPolicy.CanCancel(request.Status),
                RefundedAt = request.RefundedAt,
                RefundReference = request.RefundReference,
                RefundError = request.RefundError,
                CanRetryRefund = request.Status == ReturnRequestStatuses.RefundFailed
            };
        }
    }
}
