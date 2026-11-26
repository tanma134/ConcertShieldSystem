using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public class TicketGovernanceRepository : ITicketGovernanceRepository
    {
        private readonly TicketDbContext _context;

        public TicketGovernanceRepository(TicketDbContext context)
        {
            _context = context;
        }

        public async Task<List<Ticket>> GetActiveTicketsForEventAsync(int eventId)
        {
            return await _context.Tickets
                .Include(t => t.Order)
                .Where(t => t.EventId == eventId &&
                            !t.IsDeleted &&
                            (t.Status == null || (t.Status != "Revoked" && t.Status != "REVOKED" && t.Status != "Refunded")))
                .ToListAsync();
        }

        public async Task<int> BulkRevokeTicketsInDbAsync(
            int eventId,
            int adminId,
            TicketReconciliation reconciliation,
            List<TicketRevocation> revocations)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                // 1. Lấy và cập nhật toàn bộ vé còn hiệu lực của sự kiện sang Revoked
                var tickets = await _context.Tickets
                    .Where(t => t.EventId == eventId &&
                                !t.IsDeleted &&
                                (t.Status == null || (t.Status != "Revoked" && t.Status != "REVOKED" && t.Status != "Refunded")))
                    .ToListAsync();

                foreach (var ticket in tickets)
                {
                    ticket.Status = "Revoked";
                    ticket.IsDeleted = true;
                    ticket.DeletedAt = now;
                    ticket.DeletedBy = adminId;
                }

                // 2. Vô hiệu hóa toàn bộ Dynamic QR Tokens của các vé này
                var ticketIds = tickets.Select(t => t.TicketId).ToList();
                if (ticketIds.Count > 0)
                {
                    var qrTokens = await _context.TicketQrTokens
                        .Where(q => ticketIds.Contains(q.TicketId) && !q.IsRevoked)
                        .ToListAsync();

                    foreach (var qr in qrTokens)
                    {
                        qr.IsRevoked = true;
                        qr.RevokedAt = now;
                    }
                }

                // 3. Ghi nhận bản ghi đối soát ticket_reconciliations
                await _context.TicketReconciliations.AddAsync(reconciliation);
                await _context.SaveChangesAsync();

                // 4. Gắn reconciliation_id và lưu chi tiết vé vào ticket_revocations
                foreach (var rev in revocations)
                {
                    rev.ReconciliationId = reconciliation.ReconciliationId;
                }
                await _context.TicketRevocations.AddRangeAsync(revocations);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                return reconciliation.ReconciliationId;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<Dictionary<int, int>> GetActiveTicketCountsByEventIdsAsync(IEnumerable<int> eventIds)
        {
            var idList = eventIds.ToList();
            if (idList.Count == 0)
                return new Dictionary<int, int>();

            return await _context.Tickets
                .AsNoTracking()
                .Where(t => idList.Contains(t.EventId) &&
                            !t.IsDeleted &&
                            (t.Status == null || (t.Status != "Revoked" && t.Status != "REVOKED" && t.Status != "Refunded")))
                .GroupBy(t => t.EventId)
                .Select(g => new { EventId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EventId, x => x.Count);
        }

        public async Task<List<int>> GetCustomerIdsByEventIdAsync(int eventId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.EventId == eventId && o.CustomerId > 0 && !o.IsDeleted)
                .Select(o => o.CustomerId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<(List<TicketRevocation> Items, int TotalCount)> GetRevocationHistoryAsync(
            int? eventId,
            string? search,
            string? reasonType,
            int page,
            int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 10 : Math.Min(100, pageSize);

            // 1. Lấy danh sách từ bảng ticket_revocations
            var bulkQuery = _context.TicketRevocations.AsNoTracking().AsQueryable();

            if (eventId.HasValue && eventId.Value > 0)
            {
                bulkQuery = bulkQuery.Where(r => r.EventId == eventId.Value);
            }

            if (!string.IsNullOrWhiteSpace(reasonType) && reasonType != "ALL")
            {
                bulkQuery = bulkQuery.Where(r => r.ReasonType == reasonType);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                bulkQuery = bulkQuery.Where(r =>
                    EF.Functions.ILike(r.TicketCode, $"%{term}%") ||
                    EF.Functions.ILike(r.EventTitle, $"%{term}%") ||
                    EF.Functions.ILike(r.RevokedByName, $"%{term}%") ||
                    (r.ReasonNote != null && EF.Functions.ILike(r.ReasonNote, $"%{term}%")) ||
                    (r.CustomerName != null && EF.Functions.ILike(r.CustomerName, $"%{term}%")));
            }

            var bulkItems = await bulkQuery.ToListAsync();

            // 2. Lấy thêm các vé thu hồi đơn lẻ từ bảng status_histories (action = 'AdminRevoke')
            var singleQuery = from h in _context.StatusHistories.AsNoTracking()
                              where h.TargetType == "Ticket" && h.Action == "AdminRevoke" && h.NewStatus == "Revoked"
                              join t in _context.Tickets.AsNoTracking() on h.TargetId equals t.TicketId into tGroup
                              from t in tGroup.DefaultIfEmpty()
                              join o in _context.Orders.AsNoTracking() on h.OrderId equals o.OrderId into oGroup
                              from o in oGroup.DefaultIfEmpty()
                              select new
                              {
                                  History = h,
                                  Ticket = t,
                                  Order = o
                              };

            if (eventId.HasValue && eventId.Value > 0)
            {
                singleQuery = singleQuery.Where(x => x.History.EventId == eventId.Value);
            }

            if (!string.IsNullOrWhiteSpace(reasonType) && reasonType != "ALL")
            {
                var rt = reasonType.Trim();
                if (rt.Equals("INDIVIDUAL_REVOCATION", StringComparison.OrdinalIgnoreCase))
                {
                    // Lấy tất cả thu hồi đơn lẻ
                }
                else
                {
                    singleQuery = singleQuery.Where(x => x.History.Reason != null && EF.Functions.ILike(x.History.Reason, $"%{rt}%"));
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                Guid? termGuid = Guid.TryParse(term, out var g) ? g : null;
                int? termId = int.TryParse(term, out var id) ? id : null;

                singleQuery = singleQuery.Where(x =>
                    (x.History.Reason != null && EF.Functions.ILike(x.History.Reason, $"%{term}%")) ||
                    (x.Order != null && x.Order.FullName != null && EF.Functions.ILike(x.Order.FullName, $"%{term}%")) ||
                    (x.Order != null && x.Order.Email != null && EF.Functions.ILike(x.Order.Email, $"%{term}%")) ||
                    (x.Ticket != null && x.Ticket.OwnerName != null && EF.Functions.ILike(x.Ticket.OwnerName, $"%{term}%")) ||
                    (x.Order != null && x.Order.EventName != null && EF.Functions.ILike(x.Order.EventName, $"%{term}%")) ||
                    (termGuid.HasValue && x.Ticket != null && x.Ticket.TicketCode == termGuid.Value) ||
                    (termId.HasValue && x.History.TargetId == termId.Value));
            }

            var singleRows = await singleQuery.ToListAsync();

            // Chống trùng lặp nếu vé đã tồn tại trong ticket_revocations
            var existingCodes = new HashSet<string>(bulkItems.Select(b => b.TicketCode), StringComparer.OrdinalIgnoreCase);

            var singleItems = singleRows
                .Where(r => r.Ticket == null || !existingCodes.Contains(r.Ticket.TicketCode.ToString()))
                .Select(r => new TicketRevocation
                {
                    RevocationId = r.History.StatusHistoryId,
                    TicketCode = r.Ticket != null ? r.Ticket.TicketCode.ToString() : $"TICKET-{r.History.TargetId}",
                    EventId = r.History.EventId,
                    EventTitle = !string.IsNullOrWhiteSpace(r.Order?.EventName) ? r.Order.EventName : $"Event #{r.History.EventId}",
                    RevokedByAdminId = r.History.ActorUserId ?? 0,
                    RevokedByName = $"Admin #{r.History.ActorUserId ?? 0}",
                    RevokedAt = r.History.CreatedAt,
                    ReasonType = "INDIVIDUAL_REVOCATION",
                    ReasonNote = r.History.Reason ?? "Individual ticket revoked by admin",
                    ReconciliationId = null,
                    IsBulk = false,
                    RefundQueued = false,
                    NotificationSent = true,
                    SeatOrZone = r.Ticket?.TicketTypeName ?? "",
                    CustomerName = !string.IsNullOrWhiteSpace(r.Ticket?.OwnerName)
                        ? r.Ticket.OwnerName
                        : (!string.IsNullOrWhiteSpace(r.Order?.FullName) ? r.Order.FullName : "Customer")
                })
                .ToList();

            var combinedList = bulkItems.Concat(singleItems)
                .OrderByDescending(r => r.RevokedAt)
                .ToList();

            var totalCount = combinedList.Count;

            var pagedItems = combinedList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedItems, totalCount);
        }

        public async Task<(List<TicketReissue> Items, int TotalCount)> GetReissueHistoryAsync(
            int? eventId,
            string? search,
            int page,
            int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 10 : Math.Min(100, pageSize);

            // 1. Lấy dữ liệu trực tiếp từ bảng ticket_reissues
            var tableQuery = _context.TicketReissues.AsNoTracking().AsQueryable();

            if (eventId.HasValue && eventId.Value > 0)
            {
                tableQuery = tableQuery.Where(r => r.EventId == eventId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                tableQuery = tableQuery.Where(r =>
                    EF.Functions.ILike(r.OldTicketCode, $"%{term}%") ||
                    EF.Functions.ILike(r.NewTicketCode, $"%{term}%") ||
                    EF.Functions.ILike(r.EventTitle, $"%{term}%") ||
                    EF.Functions.ILike(r.CustomerName, $"%{term}%") ||
                    EF.Functions.ILike(r.CustomerEmail, $"%{term}%") ||
                    EF.Functions.ILike(r.Reason, $"%{term}%") ||
                    EF.Functions.ILike(r.ReissuedByName, $"%{term}%"));
            }

            var tableItems = await tableQuery.ToListAsync();

            // 2. Lấy thêm dữ liệu từ bảng status_histories (action = 'TicketReissued')
            var query = from h in _context.StatusHistories.AsNoTracking()
                        where h.TargetType == "Ticket" && h.Action == "TicketReissued" && h.NewStatus == "Revoked"
                        join oldTicket in _context.Tickets.AsNoTracking() on h.TargetId equals oldTicket.TicketId into oldTicketGroup
                        from oldTicket in oldTicketGroup.DefaultIfEmpty()
                        join order in _context.Orders.AsNoTracking() on h.OrderId equals order.OrderId into orderGroup
                        from order in orderGroup.DefaultIfEmpty()
                        select new
                        {
                            History = h,
                            OldTicket = oldTicket,
                            Order = order
                        };

            if (eventId.HasValue && eventId.Value > 0)
            {
                query = query.Where(x => x.History.EventId == eventId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                Guid? termGuid = Guid.TryParse(term, out var g) ? g : null;
                int? termId = int.TryParse(term, out var id) ? id : null;

                query = query.Where(x =>
                    (x.History.Reason != null && EF.Functions.ILike(x.History.Reason, $"%{term}%")) ||
                    (x.Order != null && x.Order.FullName != null && EF.Functions.ILike(x.Order.FullName, $"%{term}%")) ||
                    (x.Order != null && x.Order.Email != null && EF.Functions.ILike(x.Order.Email, $"%{term}%")) ||
                    (x.OldTicket != null && x.OldTicket.OwnerName != null && EF.Functions.ILike(x.OldTicket.OwnerName, $"%{term}%")) ||
                    (x.Order != null && x.Order.EventName != null && EF.Functions.ILike(x.Order.EventName, $"%{term}%")) ||
                    (termGuid.HasValue && x.OldTicket != null && x.OldTicket.TicketCode == termGuid.Value) ||
                    (termId.HasValue && x.History.TargetId == termId.Value));
            }

            var rows = await query
                .OrderByDescending(x => x.History.CreatedAt)
                .ToListAsync();

            var replacementIds = new List<int>();
            foreach (var r in rows)
            {
                var repId = ExtractReplacementTicketId(r.History.Metadata);
                if (repId.HasValue && !replacementIds.Contains(repId.Value))
                {
                    replacementIds.Add(repId.Value);
                }
            }

            var newTickets = replacementIds.Count > 0
                ? await _context.Tickets.AsNoTracking()
                    .Where(t => replacementIds.Contains(t.TicketId))
                    .ToDictionaryAsync(t => t.TicketId)
                : new Dictionary<int, Ticket>();

            var existingOldCodes = new HashSet<string>(tableItems.Select(t => t.OldTicketCode), StringComparer.OrdinalIgnoreCase);

            var histItems = rows
                .Select(r =>
                {
                    var repId = ExtractReplacementTicketId(r.History.Metadata);
                    Ticket? newTicket = null;
                    if (repId.HasValue)
                    {
                        newTickets.TryGetValue(repId.Value, out newTicket);
                    }

                    var oldCode = r.OldTicket != null ? r.OldTicket.TicketCode.ToString() : $"TICKET-{r.History.TargetId}";
                    var newCode = newTicket != null ? newTicket.TicketCode.ToString() : (repId.HasValue ? $"TICKET-{repId.Value}" : "REPLACED");

                    var customerName = !string.IsNullOrWhiteSpace(r.OldTicket?.OwnerName)
                        ? r.OldTicket.OwnerName
                        : (!string.IsNullOrWhiteSpace(r.Order?.FullName) ? r.Order.FullName : "Customer");

                    var customerEmail = r.Order?.Email ?? "";
                    var eventTitle = !string.IsNullOrWhiteSpace(r.Order?.EventName) ? r.Order.EventName : $"Event #{r.History.EventId}";
                    var seatOrZone = r.OldTicket?.TicketTypeName ?? "";

                    return new TicketReissue
                    {
                        ReissueId = 100000 + r.History.StatusHistoryId,
                        OldTicketCode = oldCode,
                        NewTicketCode = newCode,
                        EventId = r.History.EventId,
                        EventTitle = eventTitle,
                        ReissuedByAdminId = r.History.ActorUserId ?? 1,
                        ReissuedByName = (r.History.ActorUserId ?? 1) == 1 ? "Platform Administrator" : $"Admin #{r.History.ActorUserId}",
                        ReissuedAt = r.History.CreatedAt,
                        Reason = r.History.Reason ?? "Ticket Reissued",
                        CustomerName = customerName,
                        CustomerEmail = customerEmail,
                        SeatOrZone = seatOrZone,
                        DynamicQrStatus = newTicket != null ? "ACTIVE" : "REPLACED"
                    };
                })
                .Where(item => !existingOldCodes.Contains(item.OldTicketCode))
                .ToList();

            var combined = tableItems.Concat(histItems)
                .OrderByDescending(r => r.ReissuedAt)
                .ToList();

            var totalCount = combined.Count;
            var pagedItems = combined
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedItems, totalCount);
        }

        private static int? ExtractReplacementTicketId(string? metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(metadata);
                if (doc.RootElement.TryGetProperty("replacementTicketId", out var prop) && prop.TryGetInt32(out var id))
                {
                    return id;
                }
            }
            catch
            {
                // ignore parsing error
            }

            return null;
        }

        public async Task<List<TicketReconciliation>> GetReconciliationRecordsAsync(int? eventId)
        {
            var query = _context.TicketReconciliations.AsNoTracking().AsQueryable();

            if (eventId.HasValue && eventId.Value > 0)
            {
                query = query.Where(r => r.EventId == eventId.Value);
            }

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();
        }

        public async Task<(int BulkEvents, int BulkTickets, int SingleRevocations, int Reissues, int RefundQueued)> GetGovernanceStatsCountsAsync()
        {
            var bulkEvents = await _context.TicketReconciliations.CountAsync();
            var bulkTickets = await _context.TicketReconciliations.SumAsync(r => (int?)r.TotalTicketsRevoked) ?? 0;
            var singleRevocationsFromTable = await _context.TicketRevocations.CountAsync(r => !r.IsBulk);
            var singleRevocationsFromHist = await _context.StatusHistories
                .CountAsync(h => h.TargetType == "Ticket" && h.Action == "AdminRevoke" && h.NewStatus == "Revoked");
            var singleRevocations = Math.Max(singleRevocationsFromTable, singleRevocationsFromHist);
            var (reissueItems, reissueCount) = await GetReissueHistoryAsync(null, null, 1, 1);
            var reissues = reissueCount;
            var refundQueued = await _context.TicketReconciliations
                .Where(r => EF.Functions.ILike(r.RefundStatus, "%queue%"))
                .SumAsync(r => (int?)r.TotalTicketsRevoked) ?? 0;

            return (bulkEvents, bulkTickets, singleRevocations, reissues, refundQueued);
        }
    }
}
