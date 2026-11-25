using System.Net;
using AdminAPI.DTOs;
using AdminAPI.Models;

namespace AdminAPI.Repositories;

/// <summary>Thông tin block đang hiệu lực dùng cho endpoint nội bộ của TicketAPI/AuthenticationAPI.</summary>
public sealed record ActiveBlockInfo(string? Scope, DateTime? ExpiresAt, string? ReasonPublic);

public interface IRiskDecisionRepository
{
    Task<RiskDecision?> GetByIdAsync(int decisionId, bool tracked = false, CancellationToken ct = default);

    Task<List<RiskDecision>> GetByAlertAsync(int alertId, CancellationToken ct = default);

    Task<List<RiskDecision>> GetRecentByAlertAsync(int alertId, int take, CancellationToken ct = default);

    /// <summary>Decision BLOCK/HOLD gần nhất của user để hiển thị ở My Profile.</summary>
    Task<List<RiskDecision>> GetRecentForUserAsync(int userId, DateTime since, int take, CancellationToken ct = default);

    Task AddAsync(RiskDecision decision, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<RiskDecision> decisions, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>FRD_L02: user đã từng bị block kể từ mốc thời gian.</summary>
    Task<bool> HasRecentBlockAsync(int userId, DateTime since, CancellationToken ct = default);

    /// <summary>TRU_05: user từng được staff gỡ block kể từ mốc thời gian.</summary>
    Task<bool> AnyRestoredSinceAsync(int userId, DateTime since, CancellationToken ct = default);

    /// <summary>Đếm số lượt block hệ thống gần đây theo user/device/IP (phục vụ leo thang).</summary>
    Task<int> CountRecentBlocksAsync(int? userId, string? deviceFp, IPAddress? ip, DateTime since,
        CancellationToken ct = default);

    Task<(List<RiskDecision> Items, int TotalCount)> SearchBlocksAsync(RiskBlockQuery query, int page, int pageSize,
        CancellationToken ct = default);

    /// <summary>Kiểm tra trùng: mục tiêu đã có block đang hiệu lực cùng scope chưa.</summary>
    Task<bool> HasActiveBlockAsync(string scope, int? userId, int? ticketId, string? sessionId, string? deviceFp,
        IPAddress? ip, DateTime now, CancellationToken ct = default);

    /// <summary>Các scope đang bị block (khác scope đang xét) của cùng một user.</summary>
    Task<List<string>> GetActiveBlockScopesAsync(int userId, string exceptScope, DateTime now,
        CancellationToken ct = default);

    /// <summary>Còn Block/Hold đang hiệu lực (chưa hết hạn) trên cảnh báo này không.</summary>
    Task<bool> HasActiveRestrictionForAlertAsync(int alertId, DateTime now, CancellationToken ct = default);

    /// <summary>Còn Block/Hold đang Active (bỏ qua các decision đã gỡ) trên cảnh báo này không.</summary>
    Task<bool> HasActiveRestrictionForAlertExceptAsync(int alertId, IReadOnlyCollection<int> excludedDecisionIds,
        CancellationToken ct = default);

    /// <summary>Decision gốc + các decision đang hiệu lực cùng cảnh báo, dùng khi duyệt appeal chấp nhận.</summary>
    Task<List<RiskDecision>> GetForAppealResolutionAsync(int decisionId, int? alertId, CancellationToken ct = default);

    /// <summary>Worker hết hạn: còn block vé khác chưa hết hạn không.</summary>
    Task<bool> HasUnexpiredTicketBlockAsync(int ticketId, DateTime now, CancellationToken ct = default);

    /// <summary>Worker hết hạn: các block đã quá ExpiresAt (có theo dõi thay đổi).</summary>
    Task<List<RiskDecision>> GetExpiredBlocksAsync(DateTime now, int batchSize, CancellationToken ct = default);

    /// <summary>Endpoint nội bộ: block thật đang hiệu lực mới nhất cho một trong các định danh đã cho.</summary>
    Task<ActiveBlockInfo?> FindActiveBlockAsync(int? userId, IPAddress? ip, string? deviceFp, string? sessionId,
        DateTime now, CancellationToken ct = default);
}
