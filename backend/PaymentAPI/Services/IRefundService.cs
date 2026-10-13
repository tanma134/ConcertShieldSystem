using PaymentAPI.DTOs;

namespace PaymentAPI.Services
{
    public interface IRefundService
    {
        // Idempotent per return request: an already refunded request returns the stored result.
        Task<RefundResultDto> RefundAsync(CreateRefundRequestDto request, CancellationToken ct = default);
    }
}
