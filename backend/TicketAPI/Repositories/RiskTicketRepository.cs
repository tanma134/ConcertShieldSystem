using Microsoft.EntityFrameworkCore;
using TicketAPI.Domain;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public class RiskTicketRepository : IRiskTicketRepository
    {
        private readonly TicketDbContext _context;

        public RiskTicketRepository(TicketDbContext context)
        {
            _context = context;
        }

        public async Task<Ticket?> FindTicketAsync(int ticketId, CancellationToken ct = default)
        {
            return await _context.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && !t.IsDeleted, ct);
        }

        public async Task<Ticket?> GetTicketAsync(int ticketId, CancellationToken ct = default)
        {
            return await _context.Tickets
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && !t.IsDeleted, ct);
        }

        public async Task<Ticket?> GetTicketForRevokeAsync(int ticketId, CancellationToken ct = default)
        {
            return await _context.Tickets
                .Include(t => t.Order).ThenInclude(o => o.OrderDetails)
                .Include(t => t.TicketQrTokens)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && !t.IsDeleted, ct);
        }

        public async Task<bool> HasPendingReturnRequestAsync(int ticketId, CancellationToken ct = default)
        {
            return await _context.TicketReturnRequests
                .AnyAsync(r => r.TicketId == ticketId && r.Status == ReturnRequestStatuses.Pending, ct);
        }

        public async Task<TicketReturnRequest?> GetLatestReturnRequestAsync(int ticketId, CancellationToken ct = default)
        {
            return await _context.TicketReturnRequests
                .Where(r => r.TicketId == ticketId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        public void AddReturnRequest(TicketReturnRequest request)
        {
            _context.TicketReturnRequests.Add(request);
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await _context.SaveChangesAsync(ct);
        }
    }
}
