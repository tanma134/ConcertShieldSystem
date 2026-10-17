using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface IOrderRepository
    {
        Task AddCompleteOrderAsync(Order order);

        Task<Order> GetOrderByOrderId(int id);

        Task UpdateAsync(Order order);

        Task<List<Order>> GetOrdersByCustomerIdAsync(int customerId);
        Task<(List<Order> Items, int TotalCount)> GetEventOrdersAsync(
            int eventId,
            string? search,
            string? status,
            DateTime? fromDate,
            DateTime? toDateExclusive,
            int page,
            int pageSize);
    }
}
