using PaymentAPI.Models;

namespace PaymentAPI.Repositories
{
    public interface IPaymentReconciliationResolutionRepository
    {
        Task<bool> PaymentTransactionExistsAsync(int paymentTransactionId);

        Task<bool> ExistsByPaymentTransactionIdAsync(int paymentTransactionId);

        Task<HashSet<int>> GetResolvedPaymentTransactionIdsAsync();

        Task AddAsync(PaymentReconciliationResolution resolution);
    }
}
