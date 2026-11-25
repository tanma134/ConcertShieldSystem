using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

public sealed class FraudAlertRepository(AdminDbContext db) : IFraudAlertRepository
{
    private static readonly string[] OpenStatuses = { "Open", "InReview" };

    public Task<FraudAlert?> GetByIdAsync(int alertId, CancellationToken ct = default) =>
        db.FraudAlerts.AsNoTracking().FirstOrDefaultAsync(a => a.FraudAlertId == alertId, ct);

    public Task<FraudAlert?> GetForUpdateAsync(int alertId, CancellationToken ct = default) =>
        db.FraudAlerts.FirstOrDefaultAsync(a => a.FraudAlertId == alertId, ct);

    public Task<FraudAlert?> FindOpenAlertAsync(string alertType, int? userId, string? sessionId, DateTime since,
        CancellationToken ct = default)
    {
        var uid = userId;
        var sid = sessionId;
        return db.FraudAlerts.FirstOrDefaultAsync(a =>
            a.Status == "Open" && a.AlertType == alertType && a.CreatedAt >= since
            && (uid != null ? a.UserId == uid : a.SessionId == sid), ct);
    }

    public Task AddAsync(FraudAlert alert, CancellationToken ct = default) =>
        db.FraudAlerts.AddAsync(alert, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<FraudAlertMetrics> GetMetricsAsync(DateTime from, DateTime to, DateTime slaCutoff,
        CancellationToken ct = default)
    {
        var q = db.FraudAlerts.AsNoTracking().Where(a => a.CreatedAt >= from && a.CreatedAt <= to);

        var byLevel = await q.GroupBy(a => a.RiskLevel)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var byType = await q.GroupBy(a => a.AlertType)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var byStatus = await q.GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        var openHigh = await q.CountAsync(a => (a.Status == "Open" || a.Status == "InReview")
                                                && (a.RiskLevel == "High" || a.RiskLevel == "Critical"), ct);
        var overdue = await q.CountAsync(a => (a.Status == "Open" || a.Status == "InReview")
                                              && (a.RiskLevel == "High" || a.RiskLevel == "Critical")
                                              && a.CreatedAt < slaCutoff, ct);

        return new FraudAlertMetrics(
            byLevel.Select(x => (x.Key, x.Count)).ToList(),
            byType.Select(x => (x.Key, x.Count)).ToList(),
            byStatus.Select(x => (x.Key, x.Count)).ToList(),
            byLevel.Sum(x => x.Count),
            openHigh,
            overdue);
    }

    public async Task<(List<FraudAlert> Items, int TotalCount)> SearchAsync(FraudAlertQuery query, DateTime? fromUtc,
        DateTime? toUtc, int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.FraudAlerts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.RiskLevel)) q = q.Where(a => a.RiskLevel == query.RiskLevel);
        if (!string.IsNullOrWhiteSpace(query.AlertType)) q = q.Where(a => a.AlertType == query.AlertType);
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(a => a.Status == query.Status);
        if (query.UserId.HasValue) q = q.Where(a => a.UserId == query.UserId);
        if (fromUtc.HasValue) q = q.Where(a => a.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(a => a.CreatedAt <= toUtc.Value);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return (rows, total);
    }

    public async Task<(List<FraudAlert> Items, int TotalCount)> SearchCasesAsync(FraudCaseQuery query, DateTime slaCutoff,
        int? keywordId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.FraudAlerts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(a => a.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.RiskLevel)) q = q.Where(a => a.RiskLevel == query.RiskLevel);
        if (!string.IsNullOrWhiteSpace(query.AlertType)) q = q.Where(a => a.AlertType == query.AlertType);
        if (query.UserId.HasValue) q = q.Where(a => a.UserId == query.UserId);
        if (query.AssignedTo.HasValue) q = q.Where(a => a.AssignedTo == query.AssignedTo);
        if (query.Unassigned == true) q = q.Where(a => a.AssignedTo == null && OpenStatuses.Contains(a.Status));
        if (query.OverdueOnly == true)
            q = q.Where(a => OpenStatuses.Contains(a.Status)
                             && (a.RiskLevel == "High" || a.RiskLevel == "Critical") && a.CreatedAt < slaCutoff);
        if (query.From.HasValue)
        {
            var f = ToUtc(query.From.Value);
            q = q.Where(a => a.CreatedAt >= f);
        }
        if (query.To.HasValue)
        {
            var t = ToUtc(query.To.Value);
            q = q.Where(a => a.CreatedAt <= t);
        }
        if (keywordId.HasValue)
        {
            var n = keywordId.Value;
            q = q.Where(a => a.FraudAlertId == n || a.UserId == n || a.OrderId == n || a.TicketId == n);
        }

        var total = await q.CountAsync(ct);
        q = string.Equals(query.SortBy, "riskScore", StringComparison.OrdinalIgnoreCase)
            ? (query.Desc ? q.OrderByDescending(a => a.RiskScore).ThenByDescending(a => a.CreatedAt)
                          : q.OrderBy(a => a.RiskScore).ThenByDescending(a => a.CreatedAt))
            : (query.Desc ? q.OrderByDescending(a => a.CreatedAt) : q.OrderBy(a => a.CreatedAt));
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return (rows, total);
    }

    public Task<bool> IsAssignedToAsync(int alertId, int staffId, CancellationToken ct = default) =>
        db.FraudAlerts.AsNoTracking().AnyAsync(a => a.FraudAlertId == alertId && a.AssignedTo == staffId, ct);

    private static DateTime ToUtc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();
}
