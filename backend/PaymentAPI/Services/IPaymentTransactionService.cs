using PaymentAPI.Models;

namespace PaymentAPI.Services
{
    public interface IPaymentTransactionService
    {
        Task CreatePaymentTransactionAsync(long orderId, long amount);

        Task<bool> UpdatePaymentTransactionAsync(int orderId, long vnpAmount, string? gatewayTransactionRef, string status, string? responsePayload);
    }
}
