using AdminAPI.DTOs;

namespace AdminAPI.Services.Risks
{
    public interface IAppealService
    {

        Task<RiskDecisionPublicDto> GetPublicDecisionAsync(int decisionId, int requestingUserId, CancellationToken ct = default);
        Task<List<RiskDecisionPublicDto>> GetMyDecisionsAsync(int userId, CancellationToken ct = default);
        Task<RiskDecisionAdminDto> GetAdminDecisionAsync(int decisionId, CancellationToken ct = default);

        Task<AppealResultDto> SubmitAppealAsync(SubmitAppealRequest req, int userId, CancellationToken ct = default);

        Task<PagedResult<AppealListItemDto>> SearchAppealsAsync(AppealQuery query, CancellationToken ct = default);
        Task<AppealDetailDto> GetAppealDetailAsync(int appealId, CancellationToken ct = default);
        Task<AppealResultDto> ResolveAppealAsync(int appealId, ResolveAppealRequest req, int staffId, CancellationToken ct = default);
    }
}