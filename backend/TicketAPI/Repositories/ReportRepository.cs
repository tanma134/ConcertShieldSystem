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
            // A fully returned order has its status flipped to "Refunded", but it still
            // counts as money that came in: the revenue summary subtracts its refund
            // separately. Loading only "Paid" dropped the gross while keeping the
            // refund, which made the net total negative.
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                .Where(o => o.EventId == eventId && !o.IsDeleted && (o.Status == "Paid" || o.Status == "Refunded"))
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

