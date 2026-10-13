using System.Net.Http.Json;

namespace AuthenticationAPI.Services
{
    public class AuditLogClient : IAuditLogClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<AuditLogClient> _logger;

        public AuditLogClient(HttpClient http, ILogger<AuditLogClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task LogAsync(int? userId, string actorType, string action, string entityType,
            string? entityId = null, object? oldValue = null, object? newValue = null,
            string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync("/api/internal/audit-logs", new
                {
                    userId,
                    actorType,
                    action,
                    entityType,
                    entityId,
                    oldValue,
                    newValue,
                    ipAddress,
                    userAgent
                });

                if (!resp.IsSuccessStatusCode)
                    _logger.LogWarning("Audit log write returned {Status} for action {Action}", resp.StatusCode, action);
            }
            catch (Exception ex)
            {
                // Không để lỗi ghi audit log làm hỏng luồng nghiệp vụ chính (vd: login vẫn phải thành công)
                _logger.LogWarning(ex, "Failed to write audit log for action {Action}", action);
            }
        }
    }
}