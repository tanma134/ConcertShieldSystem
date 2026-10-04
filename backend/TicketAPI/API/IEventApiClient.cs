using System.Text.Json;
using BookingAPI.DTOS;
using static TicketAPI.DTOs.ConfirmEventSaleDto;

namespace TicketAPI.API
{
    public interface IEventApiClient
    {
        Task<EventApiResponseDto?> GetEventByIdAsync(int eventId);
        //Task<TicketTypeApiResponseDto?> GetTicketTypeByIdAsync(int eventId, int ticketTypeId);

        Task<bool> ConfirmPaidOrderSaleAsync(int eventId, ConfirmEventSaleRequestDto request);
    }
}
