using PaymentAPI.DTOs;

namespace PaymentAPI.API
{
    public interface ITicketApiClient
    {
        Task<bool> ConfirmOrderPaymentAsync(int orderId, ConfirmOrderPaymentRequestDTO request);

        // Same call, but also returns why TicketAPI refused (amount mismatch, EventAPI error, ...).
        Task<(bool Ok, string? Error)> ConfirmOrderPaymentWithReasonAsync(int orderId, ConfirmOrderPaymentRequestDTO request);
    }
}
