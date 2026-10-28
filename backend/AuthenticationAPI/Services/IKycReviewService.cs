using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IKycReviewService
    {
        /// <summary>Requests waiting for manual review .</summary>
        Task<KycPagedResult<KycReviewItemDto>> ListPendingAsync(int page, int pageSize);

        /// <summary> only an approved request makes the account eKYC-verified.</summary>
        Task ApproveAsync(int ekycId, int adminId, string? ip);

        Task RejectAsync(int ekycId, int adminId, string reason, string? ip);

        /// <summary> set or clear a legal hold on a user's eKYC record.</summary>
        Task SetLegalHoldAsync(int ekycId, bool hold, string? reason, int adminId, string? ip);
    }
}
