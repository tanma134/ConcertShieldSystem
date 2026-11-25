using AdminAPI.DTOs;
using AdminAPI.Models;

namespace AdminAPI.Repositories;

public sealed record FraudAlertMetrics(
    IReadOnlyList<(string Key, int Count)> ByLevel,
    IReadOnlyList<(string Key, int Count)> ByType,
    IReadOnlyList<(string Key, int Count)> ByStatus,
    int Total,
    int OpenHighOrCritical,
    int OverdueSla);

public interface IFraudAlertRepository
{
    Task<FraudAlert?> GetByIdAsync(int alertId, CancellationToken ct = default);

    /// <summary>Bản ghi có theo dõi thay đổi, dùng khi service cần cập nhật (assign/resolve/AI summary).</summary>
    Task<FraudAlert?> GetForUpdateAsync(int alertId, CancellationToken ct = default);

    /// <summary>Cảnh báo đang Open cùng loại trong cửa sổ gộp (dùng cho UpsertAlertAsync).</summary>
    Task<FraudAlert?> FindOpenAlertAsync(string alertType, int? userId, string? sessionId, DateTime since,
        CancellationToken ct = default);

    Task AddAsync(FraudAlert alert, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<FraudAlertMetrics> GetMetricsAsync(DateTime from, DateTime to, DateTime slaCutoff,
        CancellationToken ct = default);

    Task<(List<FraudAlert> Items, int TotalCount)> SearchAsync(FraudAlertQuery query, DateTime? fromUtc, DateTime? toUtc,
        int page, int pageSize, CancellationToken ct = default);

    Task<(List<FraudAlert> Items, int TotalCount)> SearchCasesAsync(FraudCaseQuery query, DateTime slaCutoff,
        int? keywordId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>BR-236: cảnh báo có được giao cho Staff này không.</summary>
    Task<bool> IsAssignedToAsync(int alertId, int staffId, CancellationToken ct = default);
}
