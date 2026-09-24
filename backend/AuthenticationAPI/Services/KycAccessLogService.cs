
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

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
    }


    public class KycAccessLogService : IKycAccessLogService
    {
        private readonly AuthenticationDbContext _db;

        public KycAccessLogService(AuthenticationDbContext db) => _db = db;

        public async Task LogAsync(int subjectUserId, int? ekycId, int? actorUserId, string actorType,
                                   string action, string? ipAddress = null, string? details = null)
        {
            _db.KycAccessLogs.Add(new KycAccessLog
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
            await _db.SaveChangesAsync();
        }

        public async Task<KycPagedResult<KycAccessLogDto>> SearchAsync(KycAccessLogQuery query)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            if (query.From.HasValue && query.To.HasValue && ToUtc(query.From.Value) > ToUtc(query.To.Value))
                throw new InvalidOperationException("'From' must not be later than 'To'");

            var q = _db.KycAccessLogs.AsNoTracking().AsQueryable();

            if (query.SubjectUserId.HasValue)
                q = q.Where(x => x.SubjectUserId == query.SubjectUserId.Value);
            if (query.ActorUserId.HasValue)
                q = q.Where(x => x.ActorUserId == query.ActorUserId.Value);
            if (!string.IsNullOrWhiteSpace(query.ActorType))
            {
                var actorType = query.ActorType.Trim();
                q = q.Where(x => x.ActorType == actorType);
            }
            if (!string.IsNullOrWhiteSpace(query.Action))
            {
                var action = query.Action.Trim().ToUpperInvariant();
                q = q.Where(x => x.Action == action);
            }
            if (query.From.HasValue)
            {
                var from = ToUtc(query.From.Value);
                q = q.Where(x => x.CreatedAt >= from);
            }
            if (query.To.HasValue)
            {
                var to = ToUtc(query.To.Value);
                q = q.Where(x => x.CreatedAt <= to);
            }

            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new KycAccessLogDto
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
                })
                .ToListAsync();

            return new KycPagedResult<KycAccessLogDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
        }

        private static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}