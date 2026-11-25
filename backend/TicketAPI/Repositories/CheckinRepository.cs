using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public class CheckinRepository : ICheckinRepository
    {
        private readonly TicketDbContext _context;

        public CheckinRepository(TicketDbContext context)
        {
            _context = context;
        }

        public async Task<Ticket?> GetTicketByCodeAsync(Guid ticketCode)
        {
            return await _context.Tickets
                .Include(t => t.Order)
                .FirstOrDefaultAsync(t => t.TicketCode == ticketCode && !t.IsDeleted);
        }

        public async Task<Ticket?> GetTicketByQrTokenAsync(string qrToken)
        {
            var qrRecord = await _context.TicketQrTokens
                .Include(q => q.Ticket)
                .ThenInclude(t => t.Order)
                .FirstOrDefaultAsync(q => q.QrToken == qrToken && !q.IsRevoked);

            if (qrRecord != null && !qrRecord.Ticket.IsDeleted)
            {
                return qrRecord.Ticket;
            }

            return null;
        }

        public async Task<Ticket?> GetTicketByIdAsync(int ticketId)
        {
            return await _context.Tickets
                .Include(t => t.Order)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && !t.IsDeleted);
        }

        public async Task AddStatusHistoryAsync(StatusHistory history)
        {
            await _context.StatusHistories.AddAsync(history);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
