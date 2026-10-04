using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Services
{
    public class AuditLogService : IAuditLogService
    {
        // Giới hạn để tránh export/query quá nặng khi bảng audit_logs phình to theo thời gian
        private const int MaxPageSize = 100;
        private const int MaxExportDays = 90;
        private const int MaxExportRows = 50_000;

        private readonly AdminDbContext _db;

        public AuditLogService(AdminDbContext db)
        {
            _db = db;
        }

        // ------------------------------------------------------------
        // 19.1 View Activity Log List
        // ------------------------------------------------------------
        public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogQuery query)
        {
            if (query.From is not null && query.To is not null && query.From > query.To)
                throw new InvalidOperationException("'from' phải nhỏ hơn hoặc bằng 'to'");

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > MaxPageSize ? 20 : query.PageSize;

            var q = BuildFilteredQuery(query);

            var totalCount = await q.LongCountAsync();

            var items = await q
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.AuditLogId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => ToDto(a))
                .ToListAsync();

            return new PagedResult<AuditLogDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // ------------------------------------------------------------
        // 19.2 Export Audit Log
        // ------------------------------------------------------------
        public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(AuditLogQuery query, string format)
        {
            if (query.From is null || query.To is null)
                throw new InvalidOperationException("Phải chỉ định khoảng thời gian 'from' và 'to' khi export");

            if (query.From > query.To)
                throw new InvalidOperationException("'from' phải nhỏ hơn hoặc bằng 'to'");

            if ((query.To.Value - query.From.Value).TotalDays > MaxExportDays)
                throw new InvalidOperationException($"Khoảng thời gian export tối đa là {MaxExportDays} ngày");

            var q = BuildFilteredQuery(query);

            var rows = await q
                .OrderBy(a => a.CreatedAt)
                .Take(MaxExportRows + 1) // +1 để phát hiện có vượt giới hạn hay không
                .Select(a => ToDto(a))
                .ToListAsync();

            if (rows.Count > MaxExportRows)
                throw new InvalidOperationException(
                    $"Kết quả vượt quá {MaxExportRows} dòng, vui lòng thu hẹp khoảng thời gian hoặc bộ lọc");

            return format.ToLowerInvariant() switch
            {
                "csv" => (BuildCsv(rows), "text/csv", $"audit-logs_{query.From:yyyyMMdd}_{query.To:yyyyMMdd}.csv"),
                _ => throw new InvalidOperationException($"Định dạng export '{format}' không được hỗ trợ (chỉ hỗ trợ: csv)")
            };
        }

        // ------------------------------------------------------------
        // Ghi log (dùng ở các service/controller khác trong AdminAPI)
        // ------------------------------------------------------------
        public async Task LogAsync(int? userId, string actorType, string action, string entityType,
            string? entityId = null, object? oldValue = null, object? newValue = null,
            string? ipAddress = null, string? requestId = null, string? userAgent = null)
        {
            var log = new AuditLog
            {
                UserId = userId,
                ActorType = actorType,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
                NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue),
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : IPAddress.Parse(ipAddress),
                RequestId = requestId,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private IQueryable<AuditLog> BuildFilteredQuery(AuditLogQuery query)
        {
            var q = _db.AuditLogs.AsNoTracking().AsQueryable();

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

        private static AuditLogDto ToDto(AuditLog a) => new()
        {
            AuditLogId = a.AuditLogId,
            UserId = a.UserId,
            ActorType = a.ActorType,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            OldValue = a.OldValue,
            NewValue = a.NewValue,
            IpAddress = a.IpAddress != null ? a.IpAddress.ToString() : null,
            RequestId = a.RequestId,
            UserAgent = a.UserAgent,
            CreatedAt = a.CreatedAt
        };

        private static byte[] BuildCsv(List<AuditLogDto> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AuditLogId,CreatedAt,UserId,ActorType,Action,EntityType,EntityId,IpAddress,RequestId,OldValue,NewValue,UserAgent");

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    r.AuditLogId,
                    r.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                    r.UserId?.ToString() ?? "",
                    Csv(r.ActorType),
                    Csv(r.Action),
                    Csv(r.EntityType),
                    Csv(r.EntityId),
                    Csv(r.IpAddress),
                    Csv(r.RequestId),
                    Csv(r.OldValue),
                    Csv(r.NewValue),
                    Csv(r.UserAgent)));
            }

            // Thêm BOM để Excel mở file UTF-8 không bị lỗi font tiếng Việt
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
            return result;
        }

        // Escape field theo chuẩn CSV: bọc "..." nếu có dấu phẩy/xuống dòng/dấu ngoặc kép
        private static string Csv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            var needsQuote = field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
            var escaped = field.Replace("\"", "\"\"");
            return needsQuote ? $"\"{escaped}\"" : escaped;
        }
    }
}