using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public class StatusHistoryRepository : IStatusHistoryRepository
    {
        private readonly TicketDbContext _context;

        public StatusHistoryRepository(TicketDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(StatusHistory history)
        {
            await _context.StatusHistories.AddAsync(history);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<StatusHistory> histories)
        {
            await _context.StatusHistories.AddRangeAsync(histories);
            await _context.SaveChangesAsync();
        }

        public async Task<List<StatusHistory>?> GetMyTicketHistoryAsync(
            int ticketId,
            int customerId)
        {
            var ticket = await _context.Tickets
                .AsNoTracking()
                .Where(t =>
                    t.TicketId == ticketId &&
                    t.OwnerUserId == customerId &&
                    !t.IsDeleted &&
                    !t.Order.IsDeleted)
                .Select(t => new
                {
                    t.OrderId,
                    t.Order.HoldToken,
                    t.TicketTypeId,
                    t.SeatId
                })
                .FirstOrDefaultAsync();

            if (ticket == null)
                return null;

            var query = _context.StatusHistories
                .AsNoTracking()
                .Where(h =>
                    h.TicketId == ticketId
                    || h.OrderId == ticket.OrderId

                    || (!string.IsNullOrEmpty(ticket.HoldToken) &&
                        h.HoldId == ticket.HoldToken &&
                        (
                            (h.TargetType == "TicketTypeInventory" &&
                             h.TicketTypeId == ticket.TicketTypeId)
                            ||
                            (ticket.SeatId.HasValue &&
                             h.TargetType == "Seat" &&
                             h.SeatId == ticket.SeatId)
                        )));

            return await query
                .OrderBy(h => h.CreatedAt)
                .ThenBy(h => h.StatusHistoryId)
                .ToListAsync();
        }

        public async Task<(List<StatusHistory> Items, int TotalCount)> GetAdminHistoryAsync(
            int? eventId,
            string? targetType,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize)
        {
            var query = _context.StatusHistories
                .AsNoTracking()
                .AsQueryable();

            if (eventId.HasValue)
                query = query.Where(h => h.EventId == eventId.Value);

            if (!string.IsNullOrWhiteSpace(targetType))
                query = query.Where(h => h.TargetType == targetType);

            if (from.HasValue)
                query = query.Where(h => h.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(h => h.CreatedAt <= to.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(h => h.CreatedAt)
                .ThenBy(h => h.StatusHistoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }

}
