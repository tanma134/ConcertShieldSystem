using TicketAPI.DTOs;

namespace TicketAPI.API
{
    public interface IPaymentAPIClient
    {
        Task<CreateVnPayPaymentResponseDto> CreateVnPayPaymentAsync(CreateVnPayPaymentRequestDto request, CancellationToken cancellationToken = default);
    }
}
