using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

/// <summary>Dòng danh sách appeal kèm mã decision để hiển thị cho Admin/Staff.</summary>
public sealed record AppealRow(RiskAppeal Appeal, string DecisionCode);

public interface IRiskAppealRepository
{
    Task<RiskAppeal?> GetByIdAsync(int appealId, bool tracked = false, CancellationToken ct = default);

    Task<RiskAppeal?> GetByDecisionAsync(int decisionId, CancellationToken ct = default);

    Task<List<RiskAppeal>> GetByDecisionIdsAsync(IReadOnlyCollection<int> decisionIds, CancellationToken ct = default);

    Task AddAsync(RiskAppeal appeal, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Dùng khi insert appeal bị lỗi unique: kiểm tra một decision đã có appeal khác chưa.</summary>
    Task<bool> AnyForDecisionAsync(int decisionId, int? exceptAppealId = null, CancellationToken ct = default);

    /// <summary>Cảnh báo còn appeal đang Pending (tuỳ chọn bỏ qua một appeal) hay không.</summary>
    Task<bool> HasPendingForAlertAsync(int alertId, int? exceptAppealId = null, CancellationToken ct = default);

    Task<(List<AppealRow> Items, int TotalCount)> SearchAsync(AppealQuery query, int page, int pageSize,
        DateTime now, CancellationToken ct = default);
}

public sealed class RiskAppealRepository(AdminDbContext db) : IRiskAppealRepository
{
    public Task<RiskAppeal?> GetByIdAsync(int appealId, bool tracked = false, CancellationToken ct = default)
    {
        var q = tracked ? db.RiskAppeals.AsQueryable() : db.RiskAppeals.AsNoTracking();
        return q.FirstOrDefaultAsync(x => x.RiskAppealId == appealId, ct);
    }

    public Task<RiskAppeal?> GetByDecisionAsync(int decisionId, CancellationToken ct = default) =>
        db.RiskAppeals.AsNoTracking().FirstOrDefaultAsync(a => a.RiskDecisionId == decisionId, ct);

    public Task<List<RiskAppeal>> GetByDecisionIdsAsync(IReadOnlyCollection<int> decisionIds,
        CancellationToken ct = default)
    {
        var ids = decisionIds.ToList();
        return db.RiskAppeals.AsNoTracking()
            .Where(a => ids.Contains(a.RiskDecisionId))
            .ToListAsync(ct);
    }

    public Task AddAsync(RiskAppeal appeal, CancellationToken ct = default) =>
        db.RiskAppeals.AddAsync(appeal, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public Task<bool> AnyForDecisionAsync(int decisionId, int? exceptAppealId = null, CancellationToken ct = default) =>
        db.RiskAppeals.AsNoTracking().AnyAsync(a =>
            a.RiskDecisionId == decisionId
            && (exceptAppealId == null || a.RiskAppealId != exceptAppealId), ct);

    public Task<bool> HasPendingForAlertAsync(int alertId, int? exceptAppealId = null,
        CancellationToken ct = default) =>
        db.RiskAppeals.AsNoTracking().AnyAsync(a =>
            a.Status == "Pending"
            && (exceptAppealId == null || a.RiskAppealId != exceptAppealId)
            && db.RiskDecisions.Any(d => d.RiskDecisionId == a.RiskDecisionId && d.FraudAlertId == alertId), ct);

    public async Task<(List<AppealRow> Items, int TotalCount)> SearchAsync(AppealQuery query, int page, int pageSize,
        DateTime now, CancellationToken ct = default)
    {
        var q = db.RiskAppeals.AsNoTracking()
            .Join(db.RiskDecisions.AsNoTracking(), a => a.RiskDecisionId, d => d.RiskDecisionId,
                (a, d) => new { Appeal = a, d.DecisionCode });

        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(x => x.Appeal.Status == query.Status);
        if (query.UserId.HasValue) q = q.Where(x => x.Appeal.UserId == query.UserId);
        if (query.OverdueOnly == true)
            q = q.Where(x => x.Appeal.Status == "Pending" && x.Appeal.SlaDueAt < now);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.Appeal.Status == "Pending" ? 0 : 1)
            .ThenBy(x => x.Appeal.SlaDueAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return (rows.Select(x => new AppealRow(x.Appeal, x.DecisionCode)).ToList(), total);
    }
}
