using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories
{
    public class PaymentTransactionRepository : IPaymentTransactionRepository
    {
        private readonly PaymentDbContext _context;

        public PaymentTransactionRepository(PaymentDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(PaymentTransaction transaction)
        {
            await _context.PaymentTransactions.AddAsync(
                transaction);

            await _context.SaveChangesAsync();
        }

        public Task<PaymentTransaction?> GetPendingByOrderIdAsync(int orderId, string gateway)
        {
            return _context.PaymentTransactions.FirstOrDefaultAsync(x =>
                x.OrderId == orderId &&
                x.Gateway == gateway &&
                x.Status == "Pending");
        }

        public async Task UpdateAsync(PaymentTransaction transaction)
        {
            await _context.SaveChangesAsync();
        }
        public Task<PaymentTransaction?> GetLatestByOrderIdAsync(int orderId, string gateway)
        {
            return _context.PaymentTransactions
                .Where(x => x.OrderId == orderId && x.Gateway == gateway)
                .OrderByDescending(x => x.RequestedAt)
                .FirstOrDefaultAsync();
        }
    }
}
