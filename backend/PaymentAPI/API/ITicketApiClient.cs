using PaymentAPI.DTOs;

namespace PaymentAPI.API
{
    public interface ITicketApiClient
    {
        Task<bool> ConfirmOrderPaymentAsync(int orderId, ConfirmOrderPaymentRequestDTO request);

        Task<OrderVoucherInfoDTO?> GetOrderVoucherInfoAsync(int orderId);

        Task<TicketOrderReconciliationInfoDto?> GetOrderReconciliationInfoAsync(int orderId);

        Task<bool> RepairPaidOrderArtifactsAsync(int orderId, ConfirmOrderPaymentRequestDTO request);

        Task<(bool Ok, string? Error)> ConfirmOrderPaymentWithReasonAsync(int orderId, ConfirmOrderPaymentRequestDTO request);
        Task<bool> CancelPendingOrderAsync(int orderId);
    }
}
