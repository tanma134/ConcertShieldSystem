using System.Collections.Generic;
using System.Threading.Tasks;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface ITicketGovernanceRepository
    {
        Task<List<Ticket>> GetActiveTicketsForEventAsync(int eventId);
        Task<int> BulkRevokeTicketsInDbAsync(int eventId, int adminId, TicketReconciliation reconciliation, List<TicketRevocation> revocations);
        Task<Dictionary<int, int>> GetActiveTicketCountsByEventIdsAsync(IEnumerable<int> eventIds);
        Task<List<int>> GetCustomerIdsByEventIdAsync(int eventId);
        Task<(List<TicketRevocation> Items, int TotalCount)> GetRevocationHistoryAsync(int? eventId, string? search, string? reasonType, int page, int pageSize);
        Task<(List<TicketReissue> Items, int TotalCount)> GetReissueHistoryAsync(int? eventId, string? search, int page, int pageSize);
        Task<List<TicketReconciliation>> GetReconciliationRecordsAsync(int? eventId);
        Task<(int BulkEvents, int BulkTickets, int SingleRevocations, int Reissues, int RefundQueued)> GetGovernanceStatsCountsAsync();
    }
}
