using PaymentAPI.Models;

namespace PaymentAPI.Repositories
{
    public interface IPaymentTransactionRepository
    {
        Task AddAsync(PaymentTransaction transaction);

        Task<PaymentTransaction?> GetPendingByOrderIdAsync(int orderId, string gateway);

        Task UpdateAsync(PaymentTransaction transaction);

        Task<PaymentTransaction?> GetLatestByOrderIdAsync(int orderId, string gateway);
    }
}
