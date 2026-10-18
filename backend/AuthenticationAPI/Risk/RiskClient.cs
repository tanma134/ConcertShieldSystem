using System.Net.Http.Json;

namespace AuthenticationAPI.Risk;

public interface IRiskClient
{
    /// <summary>Gửi rule đã khớp sang AdminAPI. Trả null nếu AdminAPI lỗi/chậm (fail-open).</summary>
    Task<RiskResult?> IngestAsync(RiskEvent evt, CancellationToken ct = default);

    /// <summary>Hỏi AdminAPI xem user/IP/device/session có đang bị block thật không.</summary>
    Task<BlockCheckResult?> CheckBlockAsync(int? userId, string? ip, string? deviceFp, string? sessionId, CancellationToken ct = default);
}

public class RiskClient : IRiskClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
    private readonly HttpClient _http;
    private readonly ILogger<RiskClient> _logger;

    public RiskClient(HttpClient http, ILogger<RiskClient> logger) { _http = http; _logger = logger; }

    public async Task<RiskResult?> IngestAsync(RiskEvent evt, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            using var resp = await _http.PostAsJsonAsync("/api/internal/fraud-alerts", evt, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Risk ingest returned {Status}: {Body}", (int)resp.StatusCode, body);
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<RiskResult>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // AdminAPI chết/chậm thì KHÔNG được làm hỏng luồng đặt vé/đăng nhập.
            _logger.LogWarning(ex, "Risk ingest failed (fail-open)");
            return null;
        }
    }

    public async Task<BlockCheckResult?> CheckBlockAsync(int? userId, string? ip, string? deviceFp, string? sessionId, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            var qs = new List<string>();
            if (userId.HasValue) qs.Add($"userId={userId.Value}");
            if (!string.IsNullOrWhiteSpace(ip)) qs.Add($"ip={Uri.EscapeDataString(ip)}");
            if (!string.IsNullOrWhiteSpace(deviceFp)) qs.Add($"deviceFp={Uri.EscapeDataString(deviceFp)}");
            if (!string.IsNullOrWhiteSpace(sessionId)) qs.Add($"sessionId={Uri.EscapeDataString(sessionId)}");
            if (qs.Count == 0) return new BlockCheckResult();

            using var resp = await _http.GetAsync("/api/internal/risk/check?" + string.Join("&", qs), cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Risk block-check returned {Status}", (int)resp.StatusCode);
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<BlockCheckResult>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Risk block-check failed (fail-open)");
            return null;
        }
    }
}
