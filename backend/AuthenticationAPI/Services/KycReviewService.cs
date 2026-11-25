using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;

namespace AuthenticationAPI.Services
{
    /// <summary>
    /// Manual review of eKYC requests whose face match score is in the uncertain range,
    /// plus legal hold management and admin access to the stored images.
    /// </summary>
    public class KycReviewService : IKycReviewService
    {
        private const string ManualReview = "ManualReview";
        private const int MaxReasonLength = 450; // FailReason column is 500 chars, "Rejected by admin: " is added

        private readonly IEkycRepository _ekyc;
        private readonly IUserRepository _users;
        private readonly IObjectStorage _storage;
        private readonly IKycAccessLogService _accessLog;
        private readonly IEmailService _email;
        private readonly ILogger<KycReviewService> _logger;

        public KycReviewService(IEkycRepository ekyc, IUserRepository users, IObjectStorage storage,
            IKycAccessLogService accessLog, IEmailService email, ILogger<KycReviewService> logger)
        {
            _ekyc = ekyc;
            _users = users;
            _storage = storage;
            _accessLog = accessLog;
            _email = email;
            _logger = logger;
        }

        public async Task<KycPagedResult<KycReviewItemDto>> ListPendingAsync(int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (rows, total) = await _ekyc.GetManualReviewPageAsync(page, pageSize);

            var items = rows.Select(e => new KycReviewItemDto
            {
                EkycId = e.EkycId,
                UserId = e.UserId,
                Status = e.Status,
                FaceMatchScore = e.FaceMatchScore,
                LivenessPassed = e.LivenessPassed,
                CreatedAt = e.CreatedAt
            }).ToList();

            return new KycPagedResult<KycReviewItemDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
        }

        public async Task ApproveAsync(int ekycId, int adminId, string? ip)
        {
            var record = await GetForReviewAsync(ekycId);

            // one CCCD can verify only one account
            if (!string.IsNullOrEmpty(record.CccdNumberHash)
                && await _ekyc.IsCccdVerifiedByAnotherUserAsync(record.CccdNumberHash, record.UserId))
                throw new InvalidOperationException("This document has already been used to verify another account");

            var now = DateTime.UtcNow;
            record.Status = "Passed";
            record.VerifiedAt = now;
            record.FailReason = null;
            record.ReviewedBy = adminId;
            record.ReviewedAt = now;

            try
            {
                // record + User.EkycStatus are saved together
                await _ekyc.UpdateAsync(record, "Passed");
            }
            catch (DuplicateRecordException)
            {
                throw new InvalidOperationException("This document has already been used to verify another account");
            }

            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                KycAccessActions.ReviewApproved, ip, "purpose=manual_review");

            await NotifyUserAsync(record.UserId, approved: true, reason: null);
        }

        public async Task RejectAsync(int ekycId, int adminId, string reason, string? ip)
        {
            reason = (reason ?? "").Trim();
            if (reason.Length == 0)
                throw new InvalidOperationException("A reason is required to reject an eKYC request");
            if (reason.Length > MaxReasonLength)
                throw new InvalidOperationException($"Reason must not exceed {MaxReasonLength} characters");

            var record = await GetForReviewAsync(ekycId);

            record.Status = "Failed";
            record.FailReason = "Rejected by admin: " + reason;
            record.VerifiedAt = null;
            record.ReviewedBy = adminId;
            record.ReviewedAt = DateTime.UtcNow;

            // the repository never downgrades a user who is already "Passed"
            await _ekyc.UpdateAsync(record, "Failed");

            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                KycAccessActions.ReviewRejected, ip, "purpose=manual_review");

            await NotifyUserAsync(record.UserId, approved: false, reason: reason);
        }

        public async Task SetLegalHoldAsync(int ekycId, bool hold, string? reason, int adminId, string? ip)
        {
            reason = (reason ?? "").Trim();
            if (hold && reason.Length == 0)
                throw new InvalidOperationException("A reason is required to place a legal hold");
            if (reason.Length > 500)
                throw new InvalidOperationException("Reason must not exceed 500 characters");

            var record = await _ekyc.GetByIdAsync(ekycId)
                ?? throw new KeyNotFoundException("eKYC record not found");

            if (hold)
            {
                if (record.DataPurgedAt is not null)
                    throw new InvalidOperationException("The data of this record has already been purged");
                record.LegalHold = true;
                record.LegalHoldReason = reason;
                record.LegalHoldAt = DateTime.UtcNow;
            }
            else
            {
                record.LegalHold = false;
                record.LegalHoldReason = null;
                record.LegalHoldAt = null;
            }

            await _ekyc.SaveChangesAsync();

            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                hold ? KycAccessActions.LegalHoldSet : KycAccessActions.LegalHoldCleared, ip,
                "purpose=legal_hold");
        }

        // ----------------------------------------------------------- Images

        public async Task<KycImageResult> GetReviewImageAsync(int ekycId, string kind, int? adminId, string? ip)
        {
            var record = await _ekyc.GetByIdAsync(ekycId);
            if (record is null)
                return KycImageResult.Failed(KycImageOutcome.NotFound);

            // images may be viewed only to review a request that is waiting for review
            if (record.Status != ManualReview)
                return KycImageResult.Failed(KycImageOutcome.NotInReview);

            var normalizedKind = kind.ToLowerInvariant();
            var key = ResolveObjectKey(record, normalizedKind);
            if (key is null)
                return KycImageResult.Failed(KycImageOutcome.NotFound);

            var stream = await _storage.DownloadAsync(key);
            if (stream is null)
                return KycImageResult.Failed(KycImageOutcome.NotFound);

            // Log BEFORE returning the image: if the access cannot be recorded, the image is not shown
            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                KycAccessActions.ViewImage, ip, $"purpose=manual_review; kind={normalizedKind}");
            _logger.LogWarning("KYC image access: admin {AdminId} viewed {Kind} of ekyc {EkycId} (user {UserId})",
                adminId, normalizedKind, ekycId, record.UserId);

            return KycImageResult.Success(stream);
        }

        public async Task<KycImageResult> GetEvidenceImageAsync(int ekycId, string kind, string reason, int? adminId, string? ip)
        {
            var record = await _ekyc.GetByIdAsync(ekycId);
            if (record is null)
                return KycImageResult.Failed(KycImageOutcome.NotFound);

            if (record.DataPurgedAt is not null)
                return KycImageResult.Failed(KycImageOutcome.Purged);

            var normalizedKind = kind.ToLowerInvariant();
            var key = ResolveObjectKey(record, normalizedKind);
            if (key is null)
                return KycImageResult.Failed(KycImageOutcome.NotFound);

            var stream = await _storage.DownloadAsync(key);
            if (stream is null)
                return KycImageResult.Failed(KycImageOutcome.NotFound);

            // Log before serving: if the access cannot be recorded, the image is not shown.
            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                KycAccessActions.ViewImage, ip,
                $"purpose=evidence_access; kind={normalizedKind}; status={record.Status}; reason={reason}");

            _logger.LogWarning("KYC evidence access: admin {AdminId} viewed {Kind} of ekyc {EkycId} (user {UserId}, status {Status})",
                adminId, normalizedKind, ekycId, record.UserId, record.Status);

            return KycImageResult.Success(stream);
        }

        private static string? ResolveObjectKey(EkycVerification record, string normalizedKind) => normalizedKind switch
        {
            "front" => record.CccdFrontObjectKey,
            "back" => record.CccdBackObjectKey,
            "selfie" => record.FaceCaptureObjectKey,
            _ => null
        };

        // ----------------------------------------------------------- Helpers

        /// <summary>
        /// Tells the customer the outcome of the manual review. Same rule as the
        /// deletion flow: a failed email must never undo an admin decision.
        /// </summary>
        private async Task NotifyUserAsync(int userId, bool approved, string? reason)
        {
            try
            {
                var email = await _users.GetEmailByIdAsync(userId);
                if (!string.IsNullOrWhiteSpace(email))
                    await _email.SendKycReviewResultAsync(email, approved, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send the eKYC review result email to user {UserId}", userId);
            }
        }

        private async Task<EkycVerification> GetForReviewAsync(int ekycId)
        {
            var record = await _ekyc.GetByIdAsync(ekycId)
                ?? throw new KeyNotFoundException("eKYC record not found");
            if (record.Status != ManualReview)
                throw new InvalidOperationException($"This request is not waiting for review (status: {record.Status})");
            return record;
        }
    }
}
