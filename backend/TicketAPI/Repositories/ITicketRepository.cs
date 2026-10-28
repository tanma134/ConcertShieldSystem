using TicketAPI.Models;
using static TicketAPI.DTOs.AdminTicketDtos;

namespace TicketAPI.Repositories
{
    public interface ITicketRepository
    {
        Task AddRangeAsync(IEnumerable<Ticket> tickets);

        Task AddQrTokensAsync(IEnumerable<TicketQrToken> qrTokens);

        Task<Ticket?> GetTicketForQrRotationAsync(int ticketId, int userId);

        Task<List<Ticket>> GetMyTicketsAsync(int userId);

        Task<Ticket?> GetMyTicketByIdAsync(int ticketId, int userId);
        Task<Ticket?> GetTicketForAdminRevokeAsync(int ticketId);

        Task RevokeTicketAsync(int ticketId, int adminUserId, string reason);
        Task<(List<Ticket> Items, int TotalCount)> GetAdminTicketsAsync(AdminTicketQueryDto query);

        Task<Ticket?> GetAdminTicketByIdAsync(int ticketId);

        Task<Ticket> ReissueTicketAsync(
            int ticketId,
            int adminUserId,
            string reason);
    }
}
