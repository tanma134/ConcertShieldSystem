
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

        private readonly AuthenticationDbContext _db;
        private readonly IKycSettingService _settings;
        private readonly IKycDataPurger _purger;
        private readonly IKycAccessLogService _accessLog;

        public KycDeletionService(AuthenticationDbContext db, IKycSettingService settings,
                                  IKycDataPurger purger, IKycAccessLogService accessLog)
        {
            _db = db;
            _settings = settings;
            _purger = purger;
            _accessLog = accessLog;
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
            var keepHash = (await _settings.GetAsync()).KeepDocumentHashAfterDeletion;

            var records = await DeletableRecords(request.UserId, keepHash).ToListAsync();
            if (records.Any(r => r.Status == "ManualReview"))
                throw new InvalidOperationException("This user has a submission under manual review. Resolve it first, or reject the request");

            foreach (var record in records)
            {
                await _purger.PurgeAsync(record, keepHash);
                await _accessLog.LogAsync(request.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                    KycAccessActions.DataPurged, ip, $"requestId={request.Id}");
            }

            request.Status = Completed;
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedBy = adminId;
            await _db.SaveChangesAsync();

            await _accessLog.LogAsync(request.UserId, null, adminId, KycActorTypes.Admin,
                KycAccessActions.DeletionApproved, ip, $"requestId={request.Id}, records={records.Count}");

            return Map(request);
        }

        public async Task<KycDeletionRequestDto> RejectAsync(int requestId, int? adminId, string note, string? ip)
        {
            note = (note ?? "").Trim();
            if (note.Length == 0)
                throw new InvalidOperationException("A reason is required to reject a deletion request");
            if (note.Length > 1000)
                throw new InvalidOperationException("Reason must not exceed 1000 characters");

            var request = await GetPendingAsync(requestId);
            request.Status = Rejected;
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedBy = adminId;
            request.Note = note;
            await _db.SaveChangesAsync();

            await _accessLog.LogAsync(request.UserId, null, adminId, KycActorTypes.Admin,
                KycAccessActions.DeletionRejected, ip, $"requestId={request.Id}");

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

        private static KycDeletionRequestDto Map(KycDeletionRequest r) => new()
        {
            Id = r.Id,
            UserId = r.UserId,
            Status = r.Status,
            RequestedAt = r.RequestedAt,
            ProcessedAt = r.ProcessedAt,
            ProcessedBy = r.ProcessedBy,
            Note = r.Note
        };
    }
}