using PaymentAPI.DTOs;

namespace PaymentAPI.API
{
    public interface ITicketApiClient
    {
        Task<bool> ConfirmOrderPaymentAsync(int orderId, ConfirmOrderPaymentRequestDTO request);
    }
}
