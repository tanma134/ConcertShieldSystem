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

        Task<List<Order>> GetExpiredPendingOrdersAsync(DateTime now);

        Task<bool> TryMarkOrderExpiredAsync(int orderId, DateTime now);

        Task<int?> GetExistingOrderIdByHoldAsync(string holdId, int customerId);

        Task<int> GetCustomerEventTicketQuantityAsync(int customerId, int eventId);
    }

}
