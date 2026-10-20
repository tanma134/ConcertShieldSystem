using TicketAPI.DTOs;
using TicketAPI.Models;

namespace TicketAPI.Services
{
    public interface IStatusHistoryService
    {
        Task RecordAsync(StatusHistory history);
        Task RecordRangeAsync(IEnumerable<StatusHistory> histories);

        Task<List<StatusHistoryResponseDto>?> GetMyTicketHistoryAsync(
            int ticketId,
            int customerId);

        Task<StatusHistoryPageDto> GetAdminHistoryAsync(
            int? eventId,
            string? targetType,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize);
    }
}
