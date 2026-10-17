using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface IStatusHistoryRepository
    {
        Task AddAsync(StatusHistory history);

        Task AddRangeAsync(IEnumerable<StatusHistory> histories);

        Task<List<StatusHistory>?> GetMyTicketHistoryAsync(int ticketId, int customerId);

        Task<(List<StatusHistory> Items, int TotalCount)> GetAdminHistoryAsync(
            int? eventId,
            string? targetType,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize);
    }
}
