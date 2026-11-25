using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public enum KycImageOutcome
    {
        Ok,
        NotFound,
        /// <summary>The record is no longer waiting for manual review, so its images may not be viewed this way.</summary>
        NotInReview,
        /// <summary>The data was already deleted.</summary>
        Purged
    }

    public class KycImageResult
    {
        public KycImageOutcome Outcome { get; init; }
        public Stream? Content { get; init; }

        public static KycImageResult Failed(KycImageOutcome outcome) => new() { Outcome = outcome };
        public static KycImageResult Success(Stream content) => new() { Outcome = KycImageOutcome.Ok, Content = content };
    }

    public interface IKycReviewService
    {
        /// <summary>Requests waiting for manual review.</summary>
        Task<KycPagedResult<KycReviewItemDto>> ListPendingAsync(int page, int pageSize);

        /// <summary>Only an approved request makes the account eKYC-verified.</summary>
        Task ApproveAsync(int ekycId, int adminId, string? ip);

        Task RejectAsync(int ekycId, int adminId, string reason, string? ip);

        /// <summary>Set or clear a legal hold on a user's eKYC record.</summary>
        Task SetLegalHoldAsync(int ekycId, bool hold, string? reason, int adminId, string? ip);

        /// <summary>Image of a request that is waiting for review. The view is logged before the image is returned.</summary>
        Task<KycImageResult> GetReviewImageAsync(int ekycId, string kind, int? adminId, string? ip);

        /// <summary>Image of any record that still has data (appeals, authority requests). The reason is logged.</summary>
        Task<KycImageResult> GetEvidenceImageAsync(int ekycId, string kind, string reason, int? adminId, string? ip);
    }
}
