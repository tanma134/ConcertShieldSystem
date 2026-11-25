using TicketAPI.DTOs;

namespace TicketAPI.API
{
    public interface IPaymentAPIClient
    {
        Task<CreateVnPayPaymentResponseDto> CreateVnPayPaymentAsync(CreateVnPayPaymentRequestDto request);
        Task<RefundOutcome> RefundAsync(int returnRequestId, int orderId, long amount, string? customerName = null, int? eventId = null, string? eventName = null, CancellationToken cancellationToken = default);

        // Records a confirmed manual bank-transfer refund in PaymentAPI for reconciliation reporting.
        Task<RefundOutcome> RecordManualRefundAsync(int returnRequestId, int orderId, long amount, string reference, int processedByUserId, string? customerName = null, int? eventId = null, string? eventName = null, string? reason = null, CancellationToken cancellationToken = default);

        Task<ApplyVoucherResponseDto> ApplyVoucherAsync(ApplyVoucherRequestDto request);

        Task<int> CancelPendingPaymentsAsync(int orderId);
    }

    public sealed record RefundOutcome(bool Success, string? Reference, string? Error);
}
