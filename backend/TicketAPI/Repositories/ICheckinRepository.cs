using System;
using System.Threading.Tasks;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface ICheckinRepository
    {
        Task<Ticket?> GetTicketByCodeAsync(Guid ticketCode);
        Task<Ticket?> GetTicketByQrTokenAsync(string qrToken);
        Task<Ticket?> GetTicketByIdAsync(int ticketId);
        Task AddStatusHistoryAsync(StatusHistory history);
        Task SaveChangesAsync();
    }
}
