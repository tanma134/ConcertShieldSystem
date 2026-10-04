using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IKycReviewService
    {
        /// <summary>Requests waiting for manual review (BR-109).</summary>
        Task<KycPagedResult<KycReviewItemDto>> ListPendingAsync(int page, int pageSize);

        /// <summary>BR-50: only an approved request makes the account eKYC-verified.</summary>
        Task ApproveAsync(int ekycId, int adminId, string? ip);

        Task RejectAsync(int ekycId, int adminId, string reason, string? ip);

        /// <summary>BR-220/222: set or clear a legal hold on a user's eKYC record.</summary>
        Task SetLegalHoldAsync(int ekycId, bool hold, string? reason, int adminId, string? ip);
    }
}
