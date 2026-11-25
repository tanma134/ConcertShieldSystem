using System.Net;
using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

public sealed class RiskDecisionRepository(AdminDbContext db) : IRiskDecisionRepository
{
    public Task<RiskDecision?> GetByIdAsync(int decisionId, bool tracked = false, CancellationToken ct = default)
    {
        var q = tracked ? db.RiskDecisions.AsQueryable() : db.RiskDecisions.AsNoTracking();
        return q.FirstOrDefaultAsync(x => x.RiskDecisionId == decisionId, ct);
    }

    public Task<List<RiskDecision>> GetByAlertAsync(int alertId, CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking()
            .Where(d => d.FraudAlertId == alertId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

    public Task<List<RiskDecision>> GetRecentByAlertAsync(int alertId, int take, CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking()
            .Where(d => d.FraudAlertId == alertId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<List<RiskDecision>> GetRecentForUserAsync(int userId, DateTime since, int take,
        CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsShadow
                        && (x.Action == "BLOCK" || x.Action == "HOLD")
                        && x.CreatedAt >= since)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task AddAsync(RiskDecision decision, CancellationToken ct = default) =>
        db.RiskDecisions.AddAsync(decision, ct).AsTask();

    public Task AddRangeAsync(IEnumerable<RiskDecision> decisions, CancellationToken ct = default) =>
        db.RiskDecisions.AddRangeAsync(decisions, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public Task<bool> HasRecentBlockAsync(int userId, DateTime since, CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking().AnyAsync(d =>
            d.UserId == userId && d.Action == "BLOCK" && !d.IsShadow && d.CreatedAt >= since
            && d.Status != "Restored" && d.Status != "Overturned", ct);

    public Task<bool> AnyRestoredSinceAsync(int userId, DateTime since, CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking().AnyAsync(d =>
            d.UserId == userId && d.RestoredAt != null && d.RestoredAt >= since, ct);

    public Task<int> CountRecentBlocksAsync(int? userId, string? deviceFp, IPAddress? ip, DateTime since,
        CancellationToken ct = default)
    {
        var uid = userId;
        var dev = deviceFp;
        return db.RiskDecisions.AsNoTracking()
            .Where(d => d.Action == "BLOCK" && !d.IsShadow && d.DecidedByType == "SYSTEM" && d.CreatedAt >= since
                        && d.Status != "Restored" && d.Status != "Overturned"
                        && ((uid != null && d.UserId == uid) || (dev != null && d.DeviceFp == dev)
                            || (uid == null && ip != null && d.IpAddress == ip)))
            .Select(d => d.CreatedAt)
            .Distinct()
            .CountAsync(ct);
    }

    public async Task<(List<RiskDecision> Items, int TotalCount)> SearchBlocksAsync(RiskBlockQuery query, int page,
        int pageSize, CancellationToken ct = default)
    {
        var q = db.RiskDecisions.AsNoTracking().Where(d => d.Action == "BLOCK");
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(d => d.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Scope)) q = q.Where(d => d.Scope == query.Scope);
        if (query.UserId.HasValue) q = q.Where(d => d.UserId == query.UserId);
        if (query.FraudAlertId.HasValue) q = q.Where(d => d.FraudAlertId == query.FraudAlertId);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return (rows, total);
    }

    public Task<bool> HasActiveBlockAsync(string scope, int? userId, int? ticketId, string? sessionId, string? deviceFp,
        IPAddress? ip, DateTime now, CancellationToken ct = default)
    {
        var activeBlocks = db.RiskDecisions.AsNoTracking().Where(x =>
            x.Action == "BLOCK" && x.Status == "Active" && !x.IsShadow && x.Scope == scope
            && (x.ExpiresAt == null || x.ExpiresAt > now));

        return scope switch
        {
            "ACCOUNT" => activeBlocks.AnyAsync(x => x.UserId == userId, ct),
            "TICKET" => activeBlocks.AnyAsync(x => x.TicketId == ticketId, ct),
            "SESSION" => activeBlocks.AnyAsync(x => x.SessionId == sessionId, ct),
            "DEVICE" => activeBlocks.AnyAsync(x => x.DeviceFp == deviceFp, ct),
            _ => activeBlocks.AnyAsync(x => x.IpAddress == ip, ct)
        };
    }

    public Task<List<string>> GetActiveBlockScopesAsync(int userId, string exceptScope, DateTime now,
        CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking()
            .Where(x => x.UserId == userId && x.Action == "BLOCK" && x.Status == "Active" && !x.IsShadow
                        && x.Scope != exceptScope && (x.ExpiresAt == null || x.ExpiresAt > now))
            .Select(x => x.Scope!)
            .Distinct()
            .ToListAsync(ct);

    public Task<bool> HasActiveRestrictionForAlertAsync(int alertId, DateTime now, CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking().AnyAsync(d =>
            d.FraudAlertId == alertId && d.Status == "Active" && !d.IsShadow
            && (d.Action == "BLOCK" || d.Action == "HOLD")
            && (d.ExpiresAt == null || d.ExpiresAt > now), ct);

    public Task<bool> HasActiveRestrictionForAlertExceptAsync(int alertId, IReadOnlyCollection<int> excludedDecisionIds,
        CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking().AnyAsync(x =>
            x.FraudAlertId == alertId && !excludedDecisionIds.Contains(x.RiskDecisionId)
            && x.Status == "Active" && !x.IsShadow && (x.Action == "BLOCK" || x.Action == "HOLD"), ct);

    public Task<List<RiskDecision>> GetForAppealResolutionAsync(int decisionId, int? alertId,
        CancellationToken ct = default) =>
        db.RiskDecisions.Where(x =>
                x.RiskDecisionId == decisionId
                || (alertId != null && x.FraudAlertId == alertId && x.Status == "Active" && !x.IsShadow
                    && (x.Action == "BLOCK" || x.Action == "HOLD")))
            .ToListAsync(ct);

    public Task<bool> HasUnexpiredTicketBlockAsync(int ticketId, DateTime now, CancellationToken ct = default) =>
        db.RiskDecisions.AsNoTracking().AnyAsync(x =>
            x.TicketId == ticketId && x.Scope == "TICKET" && x.Action == "BLOCK"
            && x.Status == "Active" && !x.IsShadow
            && x.ExpiresAt != null && x.ExpiresAt > now, ct);

    public Task<List<RiskDecision>> GetExpiredBlocksAsync(DateTime now, int batchSize,
        CancellationToken ct = default) =>
        db.RiskDecisions
            .Where(d => d.Action == "BLOCK" && d.Status == "Active" && !d.IsShadow
                        && d.ExpiresAt != null && d.ExpiresAt <= now)
            .OrderBy(d => d.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task<ActiveBlockInfo?> FindActiveBlockAsync(int? userId, IPAddress? ip, string? deviceFp,
        string? sessionId, DateTime now, CancellationToken ct = default)
    {
        var hasDevice = !string.IsNullOrWhiteSpace(deviceFp);
        var hasSession = !string.IsNullOrWhiteSpace(sessionId);

        var block = await db.RiskDecisions.AsNoTracking()
            .Where(d => d.Action == "BLOCK" && d.Status == "Active" && !d.IsShadow
                        && (d.ExpiresAt == null || d.ExpiresAt > now)
                        && ((userId != null && d.Scope == "ACCOUNT" && d.UserId == userId)
                            || (hasDevice && d.Scope == "DEVICE" && d.DeviceFp == deviceFp)
                            || (ip != null && d.Scope == "IP" && d.IpAddress == ip)
                            || (hasSession && d.Scope == "SESSION" && d.SessionId == sessionId)))
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Scope, d.ExpiresAt, d.ReasonPublic })
            .FirstOrDefaultAsync(ct);

        return block == null ? null : new ActiveBlockInfo(block.Scope, block.ExpiresAt, block.ReasonPublic);
    }
}
