using PaymentAPI.Models;

namespace PaymentAPI.Services
{
    public interface IPaymentTransactionService
    {
        Task CreatePaymentTransactionAsync(long orderId, long amount, int? userId = null, string? clientIp = null, string? deviceFp = null, string? sessionId = null);

        Task<bool> UpdatePaymentTransactionAsync(int orderId, long vnpAmount, string? gatewayTransactionRef, string status, string? responsePayload);
    }
}
