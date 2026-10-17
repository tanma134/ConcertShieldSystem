using TicketAPI.DTOs;
using static TicketAPI.DTOs.AdminTicketDtos;

namespace TicketAPI.Services
{
    public interface ITicketService
    {
        Task<List<MyTicketResponseDto>> GetMyTicketsAsync(int userId);

        Task<MyTicketResponseDto?> GetMyTicketDetailsAsync(int ticketId, int userId);
        Task RevokeSingleAsync(int ticketId, int adminUserId, string reason);
        Task<AdminTicketPageDto> GetAdminTicketsAsync(AdminTicketQueryDto query);
        Task<MyTicketResponseDto?> GetAdminTicketDetailsAsync(int ticketId);
        Task<MyTicketResponseDto> ReissueTicketAsync(
            int ticketId,
            int adminUserId,
            string reason);
    }
}
