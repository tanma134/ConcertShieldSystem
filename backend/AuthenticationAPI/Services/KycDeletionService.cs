
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AuthenticationAPI.Services
{
    public class KycDeletionService : IKycDeletionService
    {
        public const string Pending = "Pending";
        public const string Completed = "Completed";
        public const string Rejected = "Rejected";

        /// <summary>BR-56: data is deleted within 72 hours of the request.</summary>
        public const int DeletionDeadlineHours = 72;

        // BR-228: the only valid grounds for rejecting a deletion request
        public const string ReasonLegalRetention = "LegalRetention";
        public const string ReasonLegalHold = "LegalHold";
        public const string ReasonAuthorityRequest = "AuthorityRequest";
        private static readonly string[] ValidReasonCodes = { ReasonLegalRetention, ReasonLegalHold, ReasonAuthorityRequest };

        private readonly AuthenticationDbContext _db;
        private readonly IKycSettingService _settings;
        private readonly IKycDataPurger _purger;
        private readonly IKycAccessLogService _accessLog;
        private readonly IEmailService _email;
        private readonly ILogger<KycDeletionService> _logger;

        public KycDeletionService(AuthenticationDbContext db, IKycSettingService settings,
                                  IKycDataPurger purger, IKycAccessLogService accessLog,
                                  IEmailService email, ILogger<KycDeletionService> logger)
        {
            _db = db;
            _settings = settings;
            _purger = purger;
            _accessLog = accessLog;
            _email = email;
            _logger = logger;
        }

        // ------------------------------------------------------------ Người dùng

        public async Task<KycDeletionRequestDto> RequestAsync(int userId, string? ip)
        {
            if (await _db.KycDeletionRequests.AnyAsync(r => r.UserId == userId && r.Status == Pending))
                throw new InvalidOperationException("You already have a pending deletion request");

            var keepHash = (await _settings.GetAsync()).KeepDocumentHashAfterDeletion;
            if (!await DeletableRecords(userId, keepHash).AnyAsync())
                throw new InvalidOperationException("You have no eKYC data to delete");

            var request = new KycDeletionRequest
            {
                UserId = userId,
                Status = Pending,
                RequestedAt = DateTime.UtcNow
            };
            _db.KycDeletionRequests.Add(request);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // 2 request đồng thời: index unique (user_id) WHERE status='Pending' chặn cái sau
                throw new InvalidOperationException("You already have a pending deletion request");
            }

            await _accessLog.LogAsync(userId, null, userId, KycActorTypes.User,
                KycAccessActions.DeletionRequested, ip, $"requestId={request.Id}");

            return Map(request);
        }

        public async Task<List<KycDeletionRequestDto>> GetMineAsync(int userId)
        {
            var rows = await _db.KycDeletionRequests.AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
            return rows.Select(Map).ToList();
        }

        // ------------------------------------------------------------ Admin

        public async Task<KycPagedResult<KycDeletionRequestDto>> ListAsync(string? status, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var q = _db.KycDeletionRequests.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim();
                if (s != Pending && s != Completed && s != Rejected)
                    throw new InvalidOperationException("Status must be Pending, Completed or Rejected");
                q = q.Where(r => r.Status == s);
            }

            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(r => r.RequestedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return new KycPagedResult<KycDeletionRequestDto>
            {
                Items = rows.Select(Map).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<KycDeletionRequestDto> ApproveAsync(int requestId, int? adminId, string? ip)
        {
            var request = await GetPendingAsync(requestId);
            EnsureApproverIsNotRequester(request, adminId);

            var setting = await _settings.GetAsync();
            var keepHash = setting.KeepDocumentHashAfterDeletion;

            var records = await DeletableRecords(request.UserId, keepHash).ToListAsync();
            if (records.Any(r => r.Status == "ManualReview"))
                throw new InvalidOperationException("This user has a submission under manual review. Resolve it first, or reject the request");

            // BR-220 / BR-227: data under legal hold or still inside the legal retention period cannot be deleted
            if (records.Any(r => r.LegalHold))
                throw new InvalidOperationException("Some of this user's data is under legal hold. Reject the request with reason code LegalHold");
            if (records.Any(r => IsWithinLegalRetention(r, setting.MinRetentionDays)))
                throw new InvalidOperationException("Some of this user's data is still within the legally mandatory retention period. Reject the request with reason code LegalRetention");

            // BR-230: deletion and the request decision succeed or fail together
            await using var tx = await _db.Database.BeginTransactionAsync();

            foreach (var record in records)
            {
                await _purger.PurgeAsync(record, keepHash);
                await _accessLog.LogAsync(request.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                    KycAccessActions.DataPurged, ip, $"purpose=deletion_request; requestId={request.Id}");
            }

            request.Status = Completed;
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedBy = adminId;
            await _db.SaveChangesAsync();

            await _accessLog.LogAsync(request.UserId, null, adminId, KycActorTypes.Admin,
                KycAccessActions.DeletionApproved, ip, $"purpose=deletion_request; requestId={request.Id}; records={records.Count}");

            await tx.CommitAsync();

            await NotifyUserAsync(request.UserId, approved: true, reason: null);
            return Map(request);
        }

        public async Task<KycDeletionRequestDto> RejectAsync(int requestId, int? adminId, string reasonCode, string note, string? ip)
        {
            reasonCode = (reasonCode ?? "").Trim();
            note = (note ?? "").Trim();

            // BR-228: only valid grounds, and the reason must be recorded
            if (!ValidReasonCodes.Contains(reasonCode))
                throw new InvalidOperationException("Reason code must be LegalRetention, LegalHold or AuthorityRequest");
            if (note.Length == 0)
                throw new InvalidOperationException("A reason is required to reject a deletion request");
            if (note.Length > 1000)
                throw new InvalidOperationException("Reason must not exceed 1000 characters");

            var request = await GetPendingAsync(requestId);
            EnsureApproverIsNotRequester(request, adminId);

            // The stated ground must really apply to this user's data
            if (reasonCode != ReasonAuthorityRequest)
            {
                var setting = await _settings.GetAsync();
                var records = await _db.EkycVerifications.AsNoTracking()
                    .Where(e => e.UserId == request.UserId).ToListAsync();

                if (reasonCode == ReasonLegalHold && !records.Any(r => r.LegalHold))
                    throw new InvalidOperationException("None of this user's data is under legal hold");
                if (reasonCode == ReasonLegalRetention
                    && !records.Any(r => r.DataPurgedAt == null && IsWithinLegalRetention(r, setting.MinRetentionDays)))
                    throw new InvalidOperationException("None of this user's data is within the legally mandatory retention period");
            }

            request.Status = Rejected;
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedBy = adminId;
            request.ReasonCode = reasonCode;
            request.Note = note;
            await _db.SaveChangesAsync();

            await _accessLog.LogAsync(request.UserId, null, adminId, KycActorTypes.Admin,
                KycAccessActions.DeletionRejected, ip, $"purpose=deletion_request; requestId={request.Id}; reasonCode={reasonCode}");

            await NotifyUserAsync(request.UserId, approved: false, reason: note);
            return Map(request);
        }

        // ------------------------------------------------------------ Helpers

        private async Task<KycDeletionRequest> GetPendingAsync(int requestId)
        {
            var request = await _db.KycDeletionRequests.FirstOrDefaultAsync(r => r.Id == requestId)
                ?? throw new KeyNotFoundException("Deletion request not found");
            if (request.Status != Pending)
                throw new InvalidOperationException($"This request has already been processed ({request.Status})");
            return request;
        }

        /// <summary>Hồ sơ còn dữ liệu cần xóa: chưa purge, hoặc đã purge nhưng còn hash/số CCCD mà chính sách không cho giữ.</summary>
        private IQueryable<EkycVerification> DeletableRecords(int userId, bool keepHash) =>
            _db.EkycVerifications.Where(e => e.UserId == userId
                && (e.DataPurgedAt == null
                    || (!keepHash && (e.CccdNumberHash != null || e.CccdNumberEncrypted != null))));

        /// <summary>BR-229: the approver must not be the person who submitted the request.</summary>
        private static void EnsureApproverIsNotRequester(KycDeletionRequest request, int? adminId)
        {
            if (adminId is null)
                throw new InvalidOperationException("Cannot identify the approver");
            if (adminId.Value == request.UserId)
                throw new InvalidOperationException("You cannot process your own deletion request");
        }

        /// <summary>BR-227: still inside the legally mandatory retention period (counted from verification, or creation if never verified).</summary>
        private static bool IsWithinLegalRetention(EkycVerification r, int minRetentionDays) =>
            r.DataPurgedAt == null
            && (r.VerifiedAt ?? r.CreatedAt) > DateTime.UtcNow.AddDays(-minRetentionDays);

        /// <summary>BR-231: tell the user the outcome (with the reason if rejected). Never blocks the decision.</summary>
        private async Task NotifyUserAsync(int userId, bool approved, string? reason)
        {
            try
            {
                var email = await _db.Users.AsNoTracking()
                    .Where(u => u.UserId == userId).Select(u => u.Email).FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(email))
                    await _email.SendKycDeletionResultAsync(email, approved, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không gửi được email kết quả yêu cầu xóa eKYC cho user {UserId}", userId);
            }
        }

        private static KycDeletionRequestDto Map(KycDeletionRequest r) => new()
        {
            Id = r.Id,
            UserId = r.UserId,
            Status = r.Status,
            RequestedAt = r.RequestedAt,
            ProcessedAt = r.ProcessedAt,
            ProcessedBy = r.ProcessedBy,
            ReasonCode = r.ReasonCode,
            Note = r.Note,
            DueAt = r.RequestedAt.AddHours(DeletionDeadlineHours),
            IsOverdue = r.Status == Pending && DateTime.UtcNow > r.RequestedAt.AddHours(DeletionDeadlineHours)
        };
    }
}