using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CheckinAPI.Data;
using CheckinAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CheckinAPI.Repositories
{
    public class CheckinRepository : ICheckinRepository
    {
        private readonly CheckinDbContext _context;

        public CheckinRepository(CheckinDbContext context)
        {
            _context = context;
        }

        public async Task<(int TotalIssued, int CheckedIn, List<ZoneCheckinCount> ZoneCounts)> GetMonitorCountsAsync(int eventId)
        {
            var baseQuery = _context.Tickets
                .AsNoTracking()
                .Where(t => t.EventId == eventId && !t.IsDeleted);

            var totalIssued = await baseQuery.CountAsync();
            var checkedIn = await baseQuery.CountAsync(t => t.IsCheckedIn || t.Status == "Used");

            var zoneGroups = await baseQuery
                .GroupBy(t => t.TicketTypeName ?? "General Admission")
                .Select(g => new ZoneCheckinCount
                {
                    ZoneName = g.Key,
                    TotalIssued = g.Count(),
                    CheckedIn = g.Count(t => t.IsCheckedIn || t.Status == "Used")
                })
                .OrderByDescending(z => z.TotalIssued)
                .ToListAsync();

            return (totalIssued, checkedIn, zoneGroups);
        }

        public async Task<List<CheckinLog>> GetRecentScansAsync(int eventId, int limit)
        {
            return await _context.CheckinLogs
                .AsNoTracking()
                .Where(l => l.EventId == eventId)
                .OrderByDescending(l => l.ScannedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<Ticket?> GetTicketByCodeAsync(string ticketCode)
        {
            if (string.IsNullOrWhiteSpace(ticketCode))
                return null;

            var codeClean = ticketCode.Trim();

            // Support both GUID format and string matching
            if (Guid.TryParse(codeClean, out var guidCode))
            {
                return await _context.Tickets
                    .Include(t => t.Order)
                    .Include(t => t.TicketQrTokens)
                    .FirstOrDefaultAsync(t => t.TicketCode == guidCode && !t.IsDeleted);
            }

            return await _context.Tickets
                .Include(t => t.Order)
                .Include(t => t.TicketQrTokens)
                .FirstOrDefaultAsync(t => t.TicketCode.ToString() == codeClean && !t.IsDeleted);
        }

        public async Task<Ticket?> GetTicketByIdAsync(int ticketId)
        {
            return await _context.Tickets
                .Include(t => t.Order)
                .Include(t => t.TicketQrTokens)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && !t.IsDeleted);
        }

        public async Task<Ticket?> GetTicketForScanAsync(string rawPayload)
        {
            if (string.IsNullOrWhiteSpace(rawPayload))
                return null;

            var clean = rawPayload.Trim();
            var codeOnly = clean.Contains(':') ? clean.Split(':')[0].Trim() : clean;

            if (Guid.TryParse(codeOnly, out var guid))
            {
                var ticket = await _context.Tickets
                    .Include(t => t.Order)
                    .Include(t => t.TicketQrTokens)
                    .FirstOrDefaultAsync(t => t.TicketCode == guid && !t.IsDeleted);

                if (ticket != null) return ticket;
            }

            if (int.TryParse(codeOnly, out var id))
            {
                var ticket = await _context.Tickets
                    .Include(t => t.Order)
                    .Include(t => t.TicketQrTokens)
                    .FirstOrDefaultAsync(t => t.TicketId == id && !t.IsDeleted);

                if (ticket != null) return ticket;
            }

            return await _context.Tickets
                .Include(t => t.Order)
                .Include(t => t.TicketQrTokens)
                .Where(t => !t.IsDeleted && (
                    t.TicketCode.ToString() == codeOnly ||
                    t.TicketQrTokens.Any(q => q.QrToken == clean || q.QrToken == codeOnly || q.QrToken.Contains(codeOnly))))
                .FirstOrDefaultAsync();
        }

        public async Task AddCheckinLogAsync(CheckinLog log)
        {
            await _context.CheckinLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }

        public async Task SaveCheckinSuccessAsync(Ticket ticket, TicketQrToken? qrToken, CheckinLog log)
        {
            var now = DateTime.UtcNow;

            ticket.IsCheckedIn = true;
            ticket.Status = "Used";
            ticket.CheckedInAt = now;
            ticket.CheckedInBy = log.StaffUserId;
            ticket.CheckedInGate = log.GateName;
            ticket.GateId = log.GateId;
            ticket.IsInside = true;
            ticket.ReEntryCount += 1;

            if (qrToken != null)
            {
                qrToken.IsUsed = true;
                qrToken.UsedAt = now;
            }

            await _context.CheckinLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }

        public async Task SaveReEntrySuccessAsync(Ticket ticket, CheckinLog log)
        {
            ticket.IsInside = true;
            ticket.ReEntryCount += 1;

            await _context.CheckinLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }

        public async Task SaveExitSuccessAsync(Ticket ticket)
        {
            ticket.IsInside = false;
            ticket.LastExitAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<List<Ticket>> LookupTicketsAsync(
            int eventId,
            string? phone,
            string? email,
            string? attendeeName,
            string? ticketCode,
            string? cccd = null)
        {
            var query = _context.Tickets
                .AsNoTracking()
                .Include(t => t.Order!)
                    .ThenInclude(o => o.OrderAttendees)
                .Where(t => t.EventId == eventId && !t.IsDeleted);

            if (!string.IsNullOrWhiteSpace(cccd))
            {
                var term = cccd.Trim();
                query = query.Where(t => t.Order != null && t.Order.OrderAttendees.Any(a => a.CitizenId != null && EF.Functions.ILike(a.CitizenId, $"%{term}%")));
            }
            else if (!string.IsNullOrWhiteSpace(ticketCode))
            {
                var term = ticketCode.Trim();
                query = query.Where(t => EF.Functions.ILike(t.TicketCode.ToString(), $"%{term}%"));
            }
            else if (!string.IsNullOrWhiteSpace(phone))
            {
                var term = phone.Trim();
                query = query.Where(t => (t.Order != null && t.Order.Phone != null && EF.Functions.ILike(t.Order.Phone, $"%{term}%")) ||
                                         (t.Order != null && t.Order.OrderAttendees.Any(a => a.Phone != null && EF.Functions.ILike(a.Phone, $"%{term}%"))));
            }
            else if (!string.IsNullOrWhiteSpace(email))
            {
                var term = email.Trim();
                query = query.Where(t => t.Order != null && t.Order.Email != null && EF.Functions.ILike(t.Order.Email, $"%{term}%"));
            }
            else if (!string.IsNullOrWhiteSpace(attendeeName))
            {
                var term = attendeeName.Trim();
                query = query.Where(t =>
                    (t.OwnerName != null && EF.Functions.ILike(t.OwnerName, $"%{term}%")) ||
                    (t.Order != null && t.Order.FullName != null && EF.Functions.ILike(t.Order.FullName, $"%{term}%")) ||
                    (t.Order != null && t.Order.OrderAttendees.Any(a => a.FullName != null && EF.Functions.ILike(a.FullName, $"%{term}%"))));
            }

            return await query.OrderBy(t => t.TicketId).Take(50).ToListAsync();
        }

        public async Task<(List<CheckinLog> Items, int TotalCount, int ApprovedCount, int AlreadyUsedCount, int InvalidCount)> GetScanHistoryAsync(
            int eventId,
            string? gateId,
            string? statusFilter,
            int? staffUserId,
            DateTime? fromDate)
        {
            var query = _context.CheckinLogs
                .AsNoTracking()
                .Where(l => l.EventId == eventId);

            if (!string.IsNullOrWhiteSpace(gateId) && gateId != "ALL")
            {
                query = query.Where(l => l.GateId == gateId);
            }

            if (staffUserId.HasValue && staffUserId.Value > 0)
            {
                query = query.Where(l => l.StaffUserId == staffUserId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(l => l.ScannedAt >= fromDate.Value);
            }

            var totalCount = await query.CountAsync();
            var approvedCount = await query.CountAsync(l => l.Status == "APPROVED");
            var alreadyUsedCount = await query.CountAsync(l => l.Status == "ALREADY_CHECKED_IN");
            var invalidCount = await query.CountAsync(l => l.Status != "APPROVED" && l.Status != "ALREADY_CHECKED_IN");

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "ALL")
            {
                query = query.Where(l => l.Status == statusFilter);
            }

            var items = await query
                .OrderByDescending(l => l.ScannedAt)
                .Take(200)
                .ToListAsync();

            return (items, totalCount, approvedCount, alreadyUsedCount, invalidCount);
        }

        public async Task<List<Ticket>> GetOfflinePackageTicketsAsync(int eventId, List<string>? allowedTiers)
        {
            var query = _context.Tickets
                .AsNoTracking()
                .Include(t => t.TicketQrTokens)
                .Where(t => t.EventId == eventId &&
                            !t.IsDeleted &&
                            (t.Status == null || (t.Status != "Revoked" && t.Status != "REVOKED")));

            if (allowedTiers != null && allowedTiers.Count > 0)
            {
                query = query.Where(t => t.TicketTypeName != null && allowedTiers.Contains(t.TicketTypeName));
            }

            return await query.ToListAsync();
        }

        public async Task AddOfflineConflictAsync(OfflineSyncConflict conflict)
        {
            await _context.OfflineSyncConflicts.AddAsync(conflict);
            await _context.SaveChangesAsync();
        }

        public async Task<List<OfflineSyncConflict>> GetOfflineConflictsAsync(int? eventId, string? status)
        {
            var query = _context.OfflineSyncConflicts.AsNoTracking().AsQueryable();

            if (eventId.HasValue && eventId.Value > 0)
            {
                query = query.Where(c => c.EventId == eventId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                if (string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.Status == "PENDING" || c.Status == "PENDING_REVIEW");
                }
                else if (string.Equals(status, "RESOLVED", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.Status != "PENDING" && c.Status != "PENDING_REVIEW");
                }
                else
                {
                    query = query.Where(c => c.Status == status);
                }
            }

            return await query.OrderByDescending(c => c.DuplicateScannedAt).ToListAsync();
        }

        public async Task<OfflineSyncConflict?> GetOfflineConflictByIdAsync(string conflictId)
        {
            return await _context.OfflineSyncConflicts.FirstOrDefaultAsync(c => c.ConflictId == conflictId);
        }

        public async Task UpdateOfflineConflictAsync(OfflineSyncConflict conflict)
        {
            _context.OfflineSyncConflicts.Update(conflict);
            await _context.SaveChangesAsync();
        }
    }
}
