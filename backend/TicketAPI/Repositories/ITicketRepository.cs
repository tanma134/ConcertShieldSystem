using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface ITicketRepository
    {
        Task AddRangeAsync(IEnumerable<Ticket> tickets);

        Task AddQrTokensAsync(IEnumerable<TicketQrToken> qrTokens);

        Task<Ticket?> GetTicketForQrRotationAsync(int ticketId, int userId);
    }
}
