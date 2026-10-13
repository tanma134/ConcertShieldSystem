
using AdminAPI.DTOs;

namespace AdminAPI.Services
{
    public interface IAuditLogService
    {
        Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogQuery query);

        /// <summary>Trả về (fileBytes, contentType, fileName). Ném InvalidOperationException nếu query không hợp lệ (thiếu from/to, vượt quá khoảng cho phép...).</summary>
        Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(AuditLogQuery query, string format);

        Task LogAsync(int? userId, string actorType, string action, string entityType,
            string? entityId = null, object? oldValue = null, object? newValue = null,
            string? ipAddress = null, string? requestId = null, string? userAgent = null);
    }
}