using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories
{
    public class PaymentReconciliationResolutionRepository: IPaymentReconciliationResolutionRepository
    {
        private readonly PaymentDbContext _context;

        public PaymentReconciliationResolutionRepository(PaymentDbContext context)
        {
            _context = context;
        }

        public Task<bool> PaymentTransactionExistsAsync(
            int paymentTransactionId)
        {
            return _context.PaymentTransactions
                .AnyAsync(x =>
                    x.PaymentTransactionId == paymentTransactionId);
        }

        public Task<bool> ExistsByPaymentTransactionIdAsync(
            int paymentTransactionId)
        {
            return _context.PaymentReconciliationResolutions
                .AnyAsync(x =>
                    x.PaymentTransactionId == paymentTransactionId);
        }

        public async Task<HashSet<int>>
            GetResolvedPaymentTransactionIdsAsync()
        {
            var ids = await _context.PaymentReconciliationResolutions
                .AsNoTracking()
                .Select(x => x.PaymentTransactionId)
                .ToListAsync();

            return ids.ToHashSet();
        }

        public async Task AddAsync(PaymentReconciliationResolution resolution)
        {
            await _context.PaymentReconciliationResolutions.AddAsync(
                resolution);

            await _context.SaveChangesAsync();
        }
    }
}