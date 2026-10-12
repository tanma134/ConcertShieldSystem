using TicketAPI.DTOs;
using TicketAPI.Models;

namespace TicketAPI.Services
{
    public interface IOrderService
    {
        Task<int> Add(CreateOrderDTO createOrderDTO, int userId);

        Task<List<int>> GetBookedSeatIdsAsync(int eventId);

        Task<CreateVnPayPaymentRequestDto> GetOrderByOrderIdForPayment(int orderId);

        Task<Order?> GetOrderByOrderId(int orderId, int customerId);

        Task ConfirmPaymentAsync(int orderId, long amount, string transactionRef);

        Task<TicketQrResponseDto> RotateTicketQrAsync(int ticketId, int userId);

        Task<List<OrderHistoryItemDto>> GetOrderHistoryAsync(int customerId);
    }
}
