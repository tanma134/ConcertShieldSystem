using Microsoft.EntityFrameworkCore;
using TicketAPI.Domain;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly TicketDbContext _context;

        public ReportRepository(TicketDbContext context)
        {
            _context = context;
        }

        public async Task<List<Order>> GetPaidOrdersWithDetailsAsync(int eventId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                .Where(o => o.EventId == eventId && o.Status == "Paid" && !o.IsDeleted)
                .ToListAsync();
        }

        public async Task<List<TicketReturnRequest>> GetRefundableReturnRequestsAsync(int eventId)
        {
            return await _context.TicketReturnRequests
                .AsNoTracking()
                .Include(r => r.Ticket)
                .Where(r => r.EventId == eventId && 
                    (r.Status == ReturnRequestStatuses.Approved || 
                     r.Status == ReturnRequestStatuses.Refunded || 
                     r.Status == ReturnRequestStatuses.RefundFailed))
                .ToListAsync();
        }

        public async Task<List<Ticket>> GetActiveTicketsByEventIdAsync(int eventId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Where(t => t.EventId == eventId && !t.IsDeleted)
                .OrderBy(t => t.TicketId)
                .ToListAsync();
        }
    }
}

