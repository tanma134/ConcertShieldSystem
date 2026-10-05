using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface IOrderRepository
    {
        Task AddCompleteOrderAsync(Order order);

        Task<Order> GetOrderByOrderId(int id);

        Task UpdateAsync(Order order);

        Task<List<Order>> GetOrdersByCustomerIdAsync(int customerId);
    }
}
