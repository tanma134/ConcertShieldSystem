using AdminAPI.DTOs;
using AdminAPI.Models;

namespace AdminAPI.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);

    Task SaveChangesAsync();

    /// <summary>Trang danh sách audit log, mới nhất trước.</summary>
    Task<(List<AuditLog> Items, long TotalCount)> SearchAsync(AuditLogQuery query, int page, int pageSize);

    /// <summary>Dữ liệu export, cũ nhất trước; lấy tối đa <paramref name="maxRows"/> + 1 dòng để phát hiện vượt giới hạn.</summary>
    Task<List<AuditLog>> ExportAsync(AuditLogQuery query, int maxRows);

    /// <summary>User có thực hiện một trong các action kể từ mốc thời gian hay không.</summary>
    Task<bool> AnyAsync(int userId, string[] actions, DateTime since, CancellationToken ct = default);
}
