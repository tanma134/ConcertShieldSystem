using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Repositories
{
    public class KycAccessLogRepository : IKycAccessLogRepository
    {
        private readonly AuthenticationDbContext _context;

        public KycAccessLogRepository(AuthenticationDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(KycAccessLog log)
        {
            _context.KycAccessLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task<(List<KycAccessLog> Items, int Total)> SearchAsync(KycAccessLogFilter filter, int page, int pageSize)
        {
            var q = _context.KycAccessLogs.AsNoTracking().AsQueryable();

            if (filter.SubjectUserId.HasValue)
                q = q.Where(x => x.SubjectUserId == filter.SubjectUserId.Value);
            if (filter.ActorUserId.HasValue)
                q = q.Where(x => x.ActorUserId == filter.ActorUserId.Value);
            if (!string.IsNullOrEmpty(filter.ActorType))
                q = q.Where(x => x.ActorType == filter.ActorType);
            if (!string.IsNullOrEmpty(filter.Action))
                q = q.Where(x => x.Action == filter.Action);
            if (filter.FromUtc.HasValue)
                q = q.Where(x => x.CreatedAt >= filter.FromUtc.Value);
            if (filter.ToUtc.HasValue)
                q = q.Where(x => x.CreatedAt <= filter.ToUtc.Value);

            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return (items, total);
        }
    }
}
