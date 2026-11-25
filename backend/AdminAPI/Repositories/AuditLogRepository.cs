using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

public sealed class AuditLogRepository(AdminDbContext db) : IAuditLogRepository
{
    public Task AddAsync(AuditLog log) => db.AuditLogs.AddAsync(log).AsTask();

    public Task SaveChangesAsync() => db.SaveChangesAsync();

    public async Task<(List<AuditLog> Items, long TotalCount)> SearchAsync(AuditLogQuery query, int page, int pageSize)
    {
        var q = BuildFilteredQuery(query);

        var totalCount = await q.LongCountAsync();
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AuditLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<List<AuditLog>> ExportAsync(AuditLogQuery query, int maxRows) =>
        BuildFilteredQuery(query)
            .OrderBy(a => a.CreatedAt)
            .Take(maxRows + 1)
            .ToListAsync();

    public Task<bool> AnyAsync(int userId, string[] actions, DateTime since, CancellationToken ct = default) =>
        db.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.UserId == userId && actions.Contains(a.Action) && a.CreatedAt >= since, ct);

    private IQueryable<AuditLog> BuildFilteredQuery(AuditLogQuery query)
    {
        var q = db.AuditLogs.AsNoTracking().AsQueryable();

        if (query.UserId is not null)
            q = q.Where(a => a.UserId == query.UserId);

        if (!string.IsNullOrWhiteSpace(query.ActorType))
            q = q.Where(a => a.ActorType == query.ActorType);

        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(a => a.Action == query.Action);

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            q = q.Where(a => a.EntityType == query.EntityType);

        if (!string.IsNullOrWhiteSpace(query.EntityId))
            q = q.Where(a => a.EntityId == query.EntityId);

        if (query.From is not null)
        {
            // Cột created_at là "timestamp with time zone" (timestamptz) nên Npgsql bắt buộc
            // DateTime truyền vào phải có Kind=Utc. Giá trị bind từ query string (?from=2026-09-25)
            // luôn có Kind=Unspecified, nên phải ép lại trước khi dùng trong biểu thức LINQ.
            var from = DateTime.SpecifyKind(query.From.Value, DateTimeKind.Utc);
            q = q.Where(a => a.CreatedAt >= from);
        }

        if (query.To is not null)
        {
            var to = DateTime.SpecifyKind(query.To.Value, DateTimeKind.Utc);
            q = q.Where(a => a.CreatedAt <= to);
        }

        return q;
    }
}
