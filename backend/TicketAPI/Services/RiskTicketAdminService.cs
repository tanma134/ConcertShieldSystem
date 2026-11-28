using TicketAPI.API;
using TicketAPI.Domain;
using TicketAPI.Models;
using TicketAPI.Repositories;

namespace TicketAPI.Services;

public sealed record RiskTicketInfo(
    int TicketId,
    int OwnerUserId,
    int EventId,
    string? Status,
    DateTime? CheckedInAt);

public sealed record RiskRevokeResult(
    bool TicketRevoked,
    string Status,
    string RefundStatus,
    long RefundAmount,
    string? RefundReference,
    string? RefundError,
    int? ReturnRequestId);

public interface IRiskTicketAdminService
{
    Task<RiskTicketInfo?> FindTicketAsync(int ticketId, CancellationToken ct = default);
    Task BlockAsync(int ticketId, int ownerUserId, string reason, int staffId, CancellationToken ct = default);
    Task UnblockAsync(int ticketId, int ownerUserId, string reason, int staffId, CancellationToken ct = default);
    Task<RiskRevokeResult> RevokeAndRefundAsync(int ticketId, int ownerUserId, int appealId, int staffId,
        string reason, CancellationToken ct = default);
}

public sealed class RiskTicketAdminService : IRiskTicketAdminService
{
    private readonly IRiskTicketRepository _repo;
    private readonly IEventApiClient _eventApi;
    private readonly IPaymentAPIClient _payment;
    private readonly ILogger<RiskTicketAdminService> _logger;

    public RiskTicketAdminService(IRiskTicketRepository repo, IEventApiClient eventApi,
        IPaymentAPIClient payment, ILogger<RiskTicketAdminService> logger)
    {
        _repo = repo;
        _eventApi = eventApi;
        _payment = payment;
        _logger = logger;
    }

    public async Task<RiskTicketInfo?> FindTicketAsync(int ticketId, CancellationToken ct = default)
    {
        var ticket = await _repo.FindTicketAsync(ticketId, ct);
        if (ticket?.OwnerUserId == null) return null;

        return new RiskTicketInfo(ticket.TicketId, ticket.OwnerUserId.Value,
            ticket.EventId, ticket.Status, ticket.CheckedInAt);
    }

    public async Task BlockAsync(int ticketId, int ownerUserId, string reason, int staffId, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(ticketId, ownerUserId, ct);

        if (ticket.Status is TicketStatuses.Returned or TicketStatuses.Revoked)
            throw new InvalidOperationException("A returned or revoked ticket cannot be blocked.");

        if (ticket.Status == TicketStatuses.Blocked)
            return;

        ticket.Status = TicketStatuses.Blocked;
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Ticket {TicketId} blocked by risk admin {StaffId}: {Reason}", ticketId, staffId, reason);
    }

    public async Task UnblockAsync(int ticketId, int ownerUserId, string reason, int staffId, CancellationToken ct = default)
    {
        var ticket = await LoadAsync(ticketId, ownerUserId, ct);
        if (ticket.Status != TicketStatuses.Blocked)
            return;

        // If the customer had a return request open before the block, put the ticket
        // back into that state rather than silently making it usable again.
        var hasPendingReturn = await _repo.HasPendingReturnRequestAsync(ticketId, ct);

        ticket.Status = hasPendingReturn ? TicketStatuses.ReturnPending : TicketStatuses.Active;
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Ticket {TicketId} unblocked by risk admin {StaffId}: {Reason}", ticketId, staffId, reason);
    }

    public async Task<RiskRevokeResult> RevokeAndRefundAsync(int ticketId, int ownerUserId, int appealId,
        int staffId, string reason, CancellationToken ct = default)
    {
        var ticket = await _repo.GetTicketForRevokeAsync(ticketId, ct)
            ?? throw new KeyNotFoundException("Ticket not found.");

        if (ticket.OwnerUserId != ownerUserId)
            throw new KeyNotFoundException("Ticket not found.");

        var latest = await _repo.GetLatestReturnRequestAsync(ticketId, ct);

        if ((ticket.Status is TicketStatuses.Revoked or TicketStatuses.Returned) && latest != null)
            return FromExisting(latest);

        var now = DateTime.UtcNow;

        var refundAmount = ticket.CheckedInAt.HasValue ? 0 : ComputeFullRefund(ticket);

        var release = await _eventApi.TryReleaseReturnedTicketAsync(ticket.EventId, ticket.TicketTypeId, 1);
        if (!release.Success)
            _logger.LogWarning("Risk revoke {TicketId}: inventory release failed: {Error}", ticketId, release.Error);

        ticket.Status = TicketStatuses.Revoked;
        foreach (var token in ticket.TicketQrTokens.Where(t => !t.IsRevoked))
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }

        var request = latest is { Status: ReturnRequestStatuses.Pending }
            ? latest
            : new TicketReturnRequest
            {
                TicketId = ticket.TicketId,
                OrderId = ticket.OrderId,
                EventId = ticket.EventId,
                RequesterUserId = ownerUserId,
                CreatedAt = now
            };

        var storedReason = string.IsNullOrWhiteSpace(reason) ? "RISK_BLOCK_APPEAL_REJECTED" : reason;

        if (request.TicketReturnRequestId != 0 && !string.IsNullOrWhiteSpace(request.Reason))
        {
            var combined = $"{request.Reason} | {storedReason}";
            request.Reason = combined.Length > 500 ? combined[..500] : combined;
        }
        else
        {
            request.Reason = storedReason.Length > 500 ? storedReason[..500] : storedReason;
        }
        request.RefundAmount = refundAmount;
        request.Status = ReturnRequestStatuses.Approved;
        request.ReviewedBy = staffId;
        request.ReviewedAt = now;
        request.UpdatedAt = now;
        var reviewNote = $"Risk appeal #{appealId} rejected";
        request.ReviewNote = reviewNote.Length > 500 ? reviewNote[..500] : reviewNote;

        if (request.TicketReturnRequestId == 0)
            _repo.AddReturnRequest(request);

        await _repo.SaveChangesAsync(ct);

        if (refundAmount == 0)
        {
            request.Status = ReturnRequestStatuses.Refunded;
            request.RefundedAt = now;
            request.RefundReference = ticket.CheckedInAt.HasValue ? "NO-REFUND-CHECKED-IN" : "NO-REFUND";
            request.RefundError = null;
            await _repo.SaveChangesAsync(ct);
            return FromExisting(request);
        }

        var outcome = await _payment.RefundAsync(
            request.TicketReturnRequestId, request.OrderId, request.RefundAmount,
            customerName: ticket.Order?.FullName,
            eventId: request.EventId,
            eventName: ticket.Order?.EventName,
            cancellationToken: ct);
        request.UpdatedAt = DateTime.UtcNow;

        if (outcome.Success)
        {
            request.Status = ReturnRequestStatuses.Refunded;
            request.RefundedAt = DateTime.UtcNow;
            request.RefundReference = outcome.Reference;
            request.RefundError = null;
        }
        else
        {
            var error = outcome.Error ?? "Refund was not completed.";
            request.Status = ReturnRequestStatuses.RefundFailed;
            request.RefundError = error.Length > 500 ? error[..500] : error;
        }

        await _repo.SaveChangesAsync(ct);
        return FromExisting(request);
    }

    private async Task<Ticket> LoadAsync(int ticketId, int ownerUserId, CancellationToken ct)
    {
        var ticket = await _repo.GetTicketAsync(ticketId, ct)
                     ?? throw new KeyNotFoundException("Ticket not found.");
        if (ticket.OwnerUserId != ownerUserId)
            throw new KeyNotFoundException("Ticket not found.");
        return ticket;
    }

    // Full 100% of what the customer paid for this one ticket, after its share of
    // the order discount. The risk policy explicitly refunds 100% (not the event tier).
    private static long ComputeFullRefund(Ticket ticket)
    {
        var order = ticket.Order;
        if (order == null || !string.Equals(order.Status, "Paid", StringComparison.Ordinal))
            return 0;

        var detail = order.OrderDetails.FirstOrDefault(d => d.TicketTypeId == ticket.TicketTypeId);
        if (detail == null) return 0;

        var gross = order.OrderDetails.Sum(d => d.UnitPrice * d.Quantity);
        return TicketReturnPolicy.RefundAmount(detail.UnitPrice, gross, order.DiscountAmount);
    }

    private static RiskRevokeResult FromExisting(TicketReturnRequest r) => new(
        true,
        TicketStatuses.Revoked,
        r.Status,
        r.RefundAmount,
        r.RefundReference,
        r.RefundError,
        r.TicketReturnRequestId);
}
