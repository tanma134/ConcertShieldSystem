using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly TicketDbContext _context;

        public OrderRepository(TicketDbContext context)
        {
            _context = context;
        }

        public async Task AddCompleteOrderAsync(Order order)
        {
            await _context.Orders.AddAsync(order);
        }

        public async Task<Order> GetOrderByOrderId(int id)
        {
            try
            {
                var order = await _context.Orders.Include(o => o.OrderDetails).Include(o => o.OrderAttendees).FirstOrDefaultAsync(o => o.OrderId == id);

                return order;

            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }
        }

        public async Task UpdateAsync(Order order)
        {
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Order>> GetOrdersByCustomerIdAsync(int customerId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(order =>
                    order.CustomerId == customerId &&
                    !order.IsDeleted)
                .Include(order => order.OrderDetails)
                .OrderByDescending(order => order.OrderDate)
                .ToListAsync();
        }
    }
}
