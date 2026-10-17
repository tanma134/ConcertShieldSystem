using PaymentAPI.DTOs;

namespace PaymentAPI.Services
{
    public interface IPaymentReconciliationService
    {
        Task<PaymentReconciliationExceptionPageDto> GetExceptionsAsync(
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize);

        Task<PaymentReconciliationResolutionDto> ResolveExceptionAsync(
            int paymentTransactionId,
            ResolvePaymentReconciliationRequestDto request,
            int adminUserId);

        Task<PaymentReconciliationExceptionPageDto> GetFailedTransactionQueueAsync(
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize);
    }
}
