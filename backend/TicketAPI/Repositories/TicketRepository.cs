using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public class TicketRepository : ITicketRepository
    {
        private readonly TicketDbContext _context;

        public TicketRepository(TicketDbContext context)
        {
            _context = context;
        }

        public Task AddRangeAsync(IEnumerable<Ticket> tickets)
        {
            return _context.Tickets.AddRangeAsync(tickets);
        }

        public Task AddQrTokensAsync(IEnumerable<TicketQrToken> qrTokens)
        {
            return _context.TicketQrTokens.AddRangeAsync(qrTokens);
        }

        public async Task<Ticket?> GetTicketForQrRotationAsync(int ticketId, int userId)
        {
            return await _context.Tickets
                .Include(t => t.Order)
                .Include(t => t.TicketQrTokens)
                .FirstOrDefaultAsync(t =>
                    t.TicketId == ticketId &&
                    t.OwnerUserId == userId &&
                    !t.IsDeleted);
        }
    }
}
