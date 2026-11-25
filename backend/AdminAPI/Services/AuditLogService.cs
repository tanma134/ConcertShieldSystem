using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Repositories;

namespace AdminAPI.Services
{
    public class AuditLogService : IAuditLogService
    {
        // Cap export/query size as audit_logs grows over time.
        private const int MaxPageSize = 100;
        private const int MaxExportDays = 90;
        private const int MaxExportRows = 50_000;

        private readonly IAuditLogRepository _repository;

        public AuditLogService(IAuditLogRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogQuery query)
        {
            if (query.From is not null && query.To is not null && query.From > query.To)
                throw new InvalidOperationException("'from' phải nhỏ hơn hoặc bằng 'to'");

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > MaxPageSize ? 20 : query.PageSize;

            var (rows, totalCount) = await _repository.SearchAsync(query, page, pageSize);

            return new PagedResult<AuditLogDto>
            {
                Items = rows.Select(ToDto).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(AuditLogQuery query, string format)
        {
            if (query.From is null || query.To is null)
                throw new InvalidOperationException("Phải chỉ định khoảng thời gian 'from' và 'to' khi export");

            if (query.From > query.To)
                throw new InvalidOperationException("'from' phải nhỏ hơn hoặc bằng 'to'");

            if ((query.To.Value - query.From.Value).TotalDays > MaxExportDays)
                throw new InvalidOperationException($"Khoảng thời gian export tối đa là {MaxExportDays} ngày");

            var rows = (await _repository.ExportAsync(query, MaxExportRows)).Select(ToDto).ToList();

            if (rows.Count > MaxExportRows)
                throw new InvalidOperationException(
                    $"Kết quả vượt quá {MaxExportRows} dòng, vui lòng thu hẹp khoảng thời gian hoặc bộ lọc");

            return format.ToLowerInvariant() switch
            {
                "csv" => (BuildCsv(rows), "text/csv", $"audit-logs_{query.From:yyyyMMdd}_{query.To:yyyyMMdd}.csv"),
                _ => throw new InvalidOperationException($"Định dạng export '{format}' không được hỗ trợ (chỉ hỗ trợ: csv)")
            };
        }

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

            await _repository.AddAsync(log);
            await _repository.SaveChangesAsync();
        }

        // -----------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------
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

            // Prepend UTF-8 BOM so Excel opens CSV with proper encoding.
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
            return result;
        }

        private static string Csv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            var needsQuote = field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
            var escaped = field.Replace("\"", "\"\"");
            return needsQuote ? $"\"{escaped}\"" : escaped;
        }
    }
}
