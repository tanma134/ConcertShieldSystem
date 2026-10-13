using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AuthenticationAPI.Services
{
    /// <summary>
    /// Manual review of eKYC requests whose face match score is in the uncertain range (BR-109),
    /// plus legal hold management (BR-220, BR-222).
    /// </summary>
    public class KycReviewService : IKycReviewService
    {
        private const string ManualReview = "ManualReview";
        private const int MaxReasonLength = 450; // FailReason column is 500 chars, "Rejected by admin: " is added

        private readonly AuthenticationDbContext _db;
        private readonly IKycAccessLogService _accessLog;

        public KycReviewService(AuthenticationDbContext db, IKycAccessLogService accessLog)
        {
            _db = db;
            _accessLog = accessLog;
        }

        public async Task<KycPagedResult<KycReviewItemDto>> ListPendingAsync(int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var q = _db.EkycVerifications.AsNoTracking().Where(e => e.Status == ManualReview);
            var total = await q.CountAsync();
            var items = await q.OrderBy(e => e.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(e => new KycReviewItemDto
                {
                    EkycId = e.EkycId,
                    UserId = e.UserId,
                    Status = e.Status,
                    FaceMatchScore = e.FaceMatchScore,
                    LivenessPassed = e.LivenessPassed,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            return new KycPagedResult<KycReviewItemDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
        }

        public async Task ApproveAsync(int ekycId, int adminId, string? ip)
        {
            var record = await GetForReviewAsync(ekycId);

            // BR-01: one CCCD can verify only one account
            if (!string.IsNullOrEmpty(record.CccdNumberHash)
                && await _db.EkycVerifications.AnyAsync(e => e.CccdNumberHash == record.CccdNumberHash
                                                             && e.UserId != record.UserId
                                                             && e.Status == "Passed"))
                throw new InvalidOperationException("This document has already been used to verify another account");

            var now = DateTime.UtcNow;
            record.Status = "Passed";
            record.VerifiedAt = now;
            record.FailReason = null;
            record.ReviewedBy = adminId;
            record.ReviewedAt = now;

            var user = await _db.Users.FindAsync(record.UserId);
            if (user is not null)
                user.EkycStatus = "Passed";

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                throw new InvalidOperationException("This document has already been used to verify another account");
            }

            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                KycAccessActions.ReviewApproved, ip, "purpose=manual_review");
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

            var user = await _db.Users.FindAsync(record.UserId);
            if (user is not null && user.EkycStatus != "Passed")
                user.EkycStatus = "Failed";

            await _db.SaveChangesAsync();

            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                KycAccessActions.ReviewRejected, ip, "purpose=manual_review");
        }

        public async Task SetLegalHoldAsync(int ekycId, bool hold, string? reason, int adminId, string? ip)
        {
            reason = (reason ?? "").Trim();
            if (hold && reason.Length == 0)
                throw new InvalidOperationException("A reason is required to place a legal hold");
            if (reason.Length > 500)
                throw new InvalidOperationException("Reason must not exceed 500 characters");

            var record = await _db.EkycVerifications.FirstOrDefaultAsync(e => e.EkycId == ekycId)
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

            await _db.SaveChangesAsync();

            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                hold ? KycAccessActions.LegalHoldSet : KycAccessActions.LegalHoldCleared, ip,
                "purpose=legal_hold");
        }

        private async Task<EkycVerification> GetForReviewAsync(int ekycId)
        {
            var record = await _db.EkycVerifications.FirstOrDefaultAsync(e => e.EkycId == ekycId)
                ?? throw new KeyNotFoundException("eKYC record not found");
            if (record.Status != ManualReview)
                throw new InvalidOperationException($"This request is not waiting for review (status: {record.Status})");
            return record;
        }
    }
}
