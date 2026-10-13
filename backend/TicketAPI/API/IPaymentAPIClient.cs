using TicketAPI.DTOs;

namespace TicketAPI.API
{
    public interface IPaymentAPIClient
    {
        Task<CreateVnPayPaymentResponseDto> CreateVnPayPaymentAsync(CreateVnPayPaymentRequestDto request, CancellationToken cancellationToken = default);

        // Asks PaymentAPI to pay the money of an approved return back to the customer.
        Task<RefundOutcome> RefundAsync(int returnRequestId, int orderId, long amount, CancellationToken cancellationToken = default);
    }

    public sealed record RefundOutcome(bool Success, string? Reference, string? Error);
}
