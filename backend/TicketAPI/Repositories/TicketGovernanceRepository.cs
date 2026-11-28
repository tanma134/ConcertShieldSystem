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

            // 1. Optional table ticket_reissues (if present in DB)
            var tableItems = new List<TicketReissue>();
            try
            {
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
                        EF.Functions.ILike(r.Reason, $"%{term}%"));
                }
                tableItems = await tableQuery.OrderByDescending(r => r.ReissuedAt).ToListAsync();
            }
            catch
            {
                // ticket_reissues table is optional; ignore if not created
            }

            // 2. Query from status_histories
            var histQuery = _context.StatusHistories
                .AsNoTracking()
                .Where(h =>
                    h.TargetType == "Ticket" &&
                    (h.Action == "TicketReissued" || h.Action == "TICKET_REISSUED" || h.Action == "Reissued"));

            if (eventId.HasValue && eventId.Value > 0)
            {
                histQuery = histQuery.Where(h => h.EventId == eventId.Value);
            }

            var rawHistories = await histQuery
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();

            // Prefer the 'Revoked' rows (on oldTicket), fallback to 'Active' rows (on newTicket) if unpaired
            var revokedHistories = rawHistories
                .Where(h => string.Equals(h.NewStatus, "Revoked", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var pairedOldIds = new HashSet<int>(
                revokedHistories.Select(h => h.TicketId ?? h.TargetId));

            var activeHistories = rawHistories
                .Where(h => string.Equals(h.NewStatus, "Active", StringComparison.OrdinalIgnoreCase))
                .Where(h =>
                {
                    var oldId = ExtractReplacedTicketId(h.Metadata);
                    return !oldId.HasValue || !pairedOldIds.Contains(oldId.Value);
                })
                .ToList();

            var allCandidateHistories = revokedHistories.Concat(activeHistories)
                .OrderByDescending(h => h.CreatedAt)
                .ToList();

            // Gather all referenced Ticket IDs and Order IDs
            var ticketIdsToLoad = new HashSet<int>();
            var orderIdsToLoad = new HashSet<int>();
            var eventIdsToLoad = new HashSet<int>();

            foreach (var h in allCandidateHistories)
            {
                var tId = h.TicketId ?? h.TargetId;
                if (tId > 0) ticketIdsToLoad.Add(tId);

                var repId = ExtractReplacementTicketId(h.Metadata);
                if (repId.HasValue && repId.Value > 0) ticketIdsToLoad.Add(repId.Value);

                var oldId = ExtractReplacedTicketId(h.Metadata);
                if (oldId.HasValue && oldId.Value > 0) ticketIdsToLoad.Add(oldId.Value);

                if (h.OrderId.HasValue && h.OrderId.Value > 0) orderIdsToLoad.Add(h.OrderId.Value);
                if (h.EventId > 0) eventIdsToLoad.Add(h.EventId);
            }

            var ticketsDict = ticketIdsToLoad.Count > 0
                ? await _context.Tickets.AsNoTracking()
                    .Where(t => ticketIdsToLoad.Contains(t.TicketId))
                    .ToDictionaryAsync(t => t.TicketId)
                : new Dictionary<int, Ticket>();

            foreach (var t in ticketsDict.Values)
            {
                if (t.OrderId > 0) orderIdsToLoad.Add(t.OrderId);
                if (t.EventId > 0) eventIdsToLoad.Add(t.EventId);
            }

            var ordersDict = orderIdsToLoad.Count > 0
                ? await _context.Orders.AsNoTracking()
                    .Where(o => orderIdsToLoad.Contains(o.OrderId))
                    .ToDictionaryAsync(o => o.OrderId)
                : new Dictionary<int, Order>();

            // Also resolve event titles from revocations/reconciliations/orders if missing
            var eventTitleMap = new Dictionary<int, string>();
            foreach (var o in ordersDict.Values)
            {
                if (o.EventId > 0 && !string.IsNullOrWhiteSpace(o.EventName) && !eventTitleMap.ContainsKey(o.EventId))
                {
                    eventTitleMap[o.EventId] = o.EventName;
                }
            }
            if (eventIdsToLoad.Count > 0)
            {
                var revTitles = await _context.TicketRevocations.AsNoTracking()
                    .Where(r => eventIdsToLoad.Contains(r.EventId) && !string.IsNullOrEmpty(r.EventTitle))
                    .Select(r => new { r.EventId, r.EventTitle })
                    .Distinct()
                    .ToListAsync();
                foreach (var rt in revTitles)
                {
                    if (!eventTitleMap.ContainsKey(rt.EventId))
                        eventTitleMap[rt.EventId] = rt.EventTitle;
                }
            }

            var existingOldCodes = new HashSet<string>(
                tableItems.Select(t => t.OldTicketCode),
                StringComparer.OrdinalIgnoreCase);

            var histItems = new List<TicketReissue>();
            foreach (var h in allCandidateHistories)
            {
                bool isRevokedRow = string.Equals(h.NewStatus, "Revoked", StringComparison.OrdinalIgnoreCase);
                int? oldTicketId = isRevokedRow
                    ? (h.TicketId ?? h.TargetId)
                    : ExtractReplacedTicketId(h.Metadata);
                int? newTicketId = isRevokedRow
                    ? ExtractReplacementTicketId(h.Metadata)
                    : (h.TicketId ?? h.TargetId);

                Ticket? oldTicket = oldTicketId.HasValue && ticketsDict.TryGetValue(oldTicketId.Value, out var ot) ? ot : null;
                Ticket? newTicket = newTicketId.HasValue && ticketsDict.TryGetValue(newTicketId.Value, out var nt) ? nt : null;

                int? resolvedOrderId = h.OrderId ?? oldTicket?.OrderId ?? newTicket?.OrderId;
                Order? order = resolvedOrderId.HasValue && ordersDict.TryGetValue(resolvedOrderId.Value, out var ord) ? ord : null;

                var oldCode = oldTicket != null
                    ? oldTicket.TicketCode.ToString()
                    : (oldTicketId.HasValue ? $"TICKET-{oldTicketId.Value}" : $"TICKET-{h.TargetId}");

                if (existingOldCodes.Contains(oldCode))
                    continue;

                var newCode = newTicket != null
                    ? newTicket.TicketCode.ToString()
                    : (newTicketId.HasValue ? $"TICKET-{newTicketId.Value}" : "REPLACED");

                var customerName = !string.IsNullOrWhiteSpace(oldTicket?.OwnerName)
                    ? oldTicket.OwnerName
                    : !string.IsNullOrWhiteSpace(newTicket?.OwnerName)
                        ? newTicket.OwnerName
                        : (!string.IsNullOrWhiteSpace(order?.FullName) ? order.FullName : "Customer");

                var customerEmail = order?.Email ?? "";
                var evId = h.EventId > 0 ? h.EventId : (oldTicket?.EventId ?? newTicket?.EventId ?? order?.EventId ?? 0);
                var eventTitle = !string.IsNullOrWhiteSpace(order?.EventName)
                    ? order.EventName
                    : (eventTitleMap.TryGetValue(evId, out var et) ? et : $"Event #{evId}");
                var seatOrZone = oldTicket?.TicketTypeName ?? newTicket?.TicketTypeName ?? "";

                histItems.Add(new TicketReissue
                {
                    ReissueId = h.StatusHistoryId,
                    OldTicketCode = oldCode,
                    NewTicketCode = newCode,
                    EventId = evId,
                    EventTitle = eventTitle,
                    ReissuedByAdminId = h.ActorUserId ?? 0,
                    ReissuedByName = $"Admin #{h.ActorUserId ?? 1}",
                    ReissuedAt = h.CreatedAt,
                    Reason = h.Reason ?? "Ticket Reissued",
                    CustomerName = customerName,
                    CustomerEmail = customerEmail,
                    SeatOrZone = seatOrZone,
                    DynamicQrStatus = newTicket != null ? "ACTIVE" : "REPLACED"
                });
            }

            var combined = tableItems.Concat(histItems)
                .OrderByDescending(r => r.ReissuedAt)
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                combined = combined.Where(r =>
                    (r.OldTicketCode != null && r.OldTicketCode.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (r.NewTicketCode != null && r.NewTicketCode.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (r.EventTitle != null && r.EventTitle.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (r.CustomerName != null && r.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (r.CustomerEmail != null && r.CustomerEmail.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (r.Reason != null && r.Reason.Contains(term, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            var totalCount = combined.Count;
            var paged = combined
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (paged, totalCount);
        }

        private static int? ExtractReplacementTicketId(string? metadata)
        {
            return ExtractJsonInt(metadata, "replacementTicketId", "ReplacementTicketId");
        }

        private static int? ExtractReplacedTicketId(string? metadata)
        {
            return ExtractJsonInt(metadata, "replacedTicketId", "ReplacedTicketId");
        }

        private static int? ExtractJsonInt(string? metadata, params string[] keys)
        {
            if (string.IsNullOrWhiteSpace(metadata))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(metadata);
                foreach (var key in keys)
                {
                    if (doc.RootElement.TryGetProperty(key, out var prop))
                    {
                        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var id))
                            return id;
                        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var strId))
                            return strId;
                    }
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

            var reissuesFromHist = await _context.StatusHistories
                .CountAsync(h =>
                    h.TargetType == "Ticket" &&
                    (h.Action == "TicketReissued" || h.Action == "TICKET_REISSUED" || h.Action == "Reissued") &&
                    (h.NewStatus == "Revoked" || h.NewStatus == "REVOKED"));

            var reissuesFromTable = 0;
            try
            {
                reissuesFromTable = await _context.TicketReissues.CountAsync();
            }
            catch
            {
                // optional table
            }

            var reissues = Math.Max(reissuesFromTable, reissuesFromHist);
            var refundQueued = await _context.TicketReconciliations
                .Where(r => EF.Functions.ILike(r.RefundStatus, "%queue%"))
                .SumAsync(r => (int?)r.TotalTicketsRevoked) ?? 0;

            return (bulkEvents, bulkTickets, singleRevocations, reissues, refundQueued);
        }
    }
}
