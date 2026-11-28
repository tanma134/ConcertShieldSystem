using System.Collections.Generic;
using System.Threading.Tasks;
using TicketAPI.DTOs;

namespace TicketAPI.Services
{
    public interface ITicketGovernanceService
    {
        Task<BulkRevokeResponseDTO> BulkRevokeTicketsAsync(BulkRevokeRequestDTO request, int adminId, string adminName);
        Task<RevocationHistoryResponseDTO> GetRevocationHistoryAsync(int? eventId, string? search, string? reasonType, int page, int pageSize);
        Task<ReissueHistoryResponseDTO> GetReissueHistoryAsync(int? eventId, string? search, int page, int pageSize, string callerRole = "Admin");
        Task<List<ReconciliationRecordDTO>> GetReconciliationRecordsAsync(int? eventId);
        Task<GovernanceStatsDTO> GetGovernanceStatsAsync();
        Task<List<EventSelectionDTO>> GetEventsForGovernanceAsync();
    }
}
