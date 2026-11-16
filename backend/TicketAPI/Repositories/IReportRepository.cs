using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface IReportRepository
    {
        Task<List<Order>> GetPaidOrdersWithDetailsAsync(int eventId);
        Task<List<TicketReturnRequest>> GetRefundableReturnRequestsAsync(int eventId);
        Task<List<Ticket>> GetActiveTicketsByEventIdAsync(int eventId);
    }
}

