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
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .Include(o => o.OrderAttendees)
                    .Include(o => o.Tickets)
                        .ThenInclude(t => t.TicketQrTokens)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(o => o.OrderId == id);

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

        public async Task<(List<Order> Items, int TotalCount)> GetEventOrdersAsync(
            int eventId,
            string? search,
            string? status,
            DateTime? fromDate,
            DateTime? toDateExclusive,
            int page,
            int pageSize)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Where(order =>
                    order.EventId == eventId &&
                    !order.IsDeleted);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(order => order.Status == status.Trim());
            }

            if (fromDate.HasValue)
            {
                query = query.Where(order =>
                    order.OrderDate.HasValue &&
                    order.OrderDate.Value >= fromDate.Value);
            }

            if (toDateExclusive.HasValue)
            {
                query = query.Where(order =>
                    order.OrderDate.HasValue &&
                    order.OrderDate.Value < toDateExclusive.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();

                if (int.TryParse(term, out var orderId))
                {
                    query = query.Where(order =>
                        order.OrderId == orderId ||
                        (order.FullName != null && order.FullName.Contains(term)) ||
                        (order.Email != null && order.Email.Contains(term)));
                }
                else
                {
                    query = query.Where(order =>
                        (order.FullName != null && order.FullName.Contains(term)) ||
                        (order.Email != null && order.Email.Contains(term)));
                }
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .Include(order => order.OrderDetails)
                .OrderByDescending(order => order.OrderDate)
                .ThenByDescending(order => order.OrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
