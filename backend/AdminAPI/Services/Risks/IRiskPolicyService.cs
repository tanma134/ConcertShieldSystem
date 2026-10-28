using AdminAPI.DTOs;

namespace AdminAPI.Services.Risks
{
    public interface IRiskPolicyService
    {
        Task<List<RiskPolicyItemDto>> GetAsync(string? category);
        Task<UpdateRiskPolicyResult> UpdateAsync(UpdateRiskPolicyRequest req, int adminId);
    }
}
