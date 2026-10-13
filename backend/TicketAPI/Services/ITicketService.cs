using TicketAPI.DTOs;

namespace TicketAPI.Services
{
    public interface ITicketService
    {
        Task<List<MyTicketResponseDto>> GetMyTicketsAsync(int userId);

        Task<MyTicketResponseDto?> GetMyTicketDetailsAsync(int ticketId, int userId);
    }
}
