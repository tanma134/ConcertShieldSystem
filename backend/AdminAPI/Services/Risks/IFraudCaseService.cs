using AdminAPI.DTOs;

namespace AdminAPI.Services.Risks
{
    public interface IFraudCaseService
    {
        Task<PagedResult<FraudCaseListItemDto>> SearchAsync(FraudCaseQuery q);
        Task<FraudCaseResultDto> AssignAsync(int caseId, int analystId, int actorId);
        Task<FraudCaseResultDto> ResolveAsync(int caseId, string outcome, string note, int actorId);
    }
}