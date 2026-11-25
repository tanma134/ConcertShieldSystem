
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;

namespace AuthenticationAPI.Services
{
    public static class KycActorTypes
    {
        public const string User = "User";
        public const string Admin = "Admin";
        public const string System = "System";
    }

    public static class KycAccessActions
    {
        public const string ViewImage = "VIEW_IMAGE";
        public const string RetentionPurge = "RETENTION_PURGE";
        public const string DeletionRequested = "DELETION_REQUESTED";
        public const string DeletionApproved = "DELETION_APPROVED";
        public const string DeletionRejected = "DELETION_REJECTED";
        public const string DataPurged = "DATA_PURGED";
        public const string Submit = "SUBMIT";
        public const string SharedWithProvider = "SHARED_WITH_PROVIDER";
        public const string ReviewApproved = "REVIEW_APPROVED";
        public const string ReviewRejected = "REVIEW_REJECTED";
        public const string LegalHoldSet = "LEGAL_HOLD_SET";
        public const string LegalHoldCleared = "LEGAL_HOLD_CLEARED";
    }


    public class KycAccessLogService : IKycAccessLogService
    {
        private readonly IKycAccessLogRepository _repository;

        public KycAccessLogService(IKycAccessLogRepository repository) => _repository = repository;

        public async Task LogAsync(int subjectUserId, int? ekycId, int? actorUserId, string actorType,
                                   string action, string? ipAddress = null, string? details = null)
        {
            await _repository.CreateAsync(new KycAccessLog
            {
                SubjectUserId = subjectUserId,
                EkycId = ekycId,
                ActorUserId = actorUserId,
                ActorType = actorType,
                Action = action,
                IpAddress = ipAddress,
                Details = details,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<KycPagedResult<KycAccessLogDto>> SearchAsync(KycAccessLogQuery query)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            if (query.From.HasValue && query.To.HasValue && ToUtc(query.From.Value) > ToUtc(query.To.Value))
                throw new InvalidOperationException("'From' must not be later than 'To'");

            var filter = new KycAccessLogFilter
            {
                SubjectUserId = query.SubjectUserId,
                ActorUserId = query.ActorUserId,
                ActorType = string.IsNullOrWhiteSpace(query.ActorType) ? null : query.ActorType.Trim(),
                Action = string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim().ToUpperInvariant(),
                FromUtc = query.From.HasValue ? ToUtc(query.From.Value) : null,
                ToUtc = query.To.HasValue ? ToUtc(query.To.Value) : null
            };

            var (rows, total) = await _repository.SearchAsync(filter, page, pageSize);

            var items = rows.Select(x => new KycAccessLogDto
            {
                Id = x.Id,
                SubjectUserId = x.SubjectUserId,
                EkycId = x.EkycId,
                ActorUserId = x.ActorUserId,
                ActorType = x.ActorType,
                Action = x.Action,
                IpAddress = x.IpAddress,
                Details = x.Details,
                CreatedAt = x.CreatedAt
            }).ToList();

            return new KycPagedResult<KycAccessLogDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
        }

        public async Task<KycPagedResult<KycMyAccessLogDto>> SearchMineAsync(int userId, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (rows, total) = await _repository.SearchAsync(
                new KycAccessLogFilter { SubjectUserId = userId }, page, pageSize);

            return new KycPagedResult<KycMyAccessLogDto>
            {
                Items = rows.Select(r => new KycMyAccessLogDto
                {
                    Id = r.Id,
                    ActorType = r.ActorType,
                    Action = r.Action,
                    Purpose = ExtractPurpose(r.Details),
                    CreatedAt = r.CreatedAt
                }).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <summary>Details are written as "purpose=xxx; key=value; ..." .</summary>
        private static string? ExtractPurpose(string? details)
        {
            if (string.IsNullOrWhiteSpace(details)) return null;
            foreach (var part in details.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Trim();
                if (kv.StartsWith("purpose=", StringComparison.OrdinalIgnoreCase))
                    return kv["purpose=".Length..].Trim();
            }
            return null;
        }

        private static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}