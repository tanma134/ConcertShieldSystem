using System.Net;

namespace TicketAPI.Risk;

/// <summary>Tín hiệu lấy từ request: IP thật, User-Agent, và các header frontend gửi lên (xem riskSignals.js).</summary>
public sealed class RiskRequestContext
{
    public string? Ip { get; init; }
    public string UserAgent { get; init; } = "";
    public string? DeviceFp { get; init; }
    public string? SessionId { get; init; }
    public bool Webdriver { get; init; }
    /// <summary>true/false nếu frontend gửi X-Had-Input; null nếu không gửi (không biết).</summary>
    public bool? HadInput { get; init; }
    public HttpRequest Request { get; init; } = null!;

    public static RiskRequestContext From(HttpContext http)
    {
        var req = http.Request;
        return new RiskRequestContext
        {
            Request = req,
            Ip = ResolveIp(http),
            UserAgent = req.Headers.UserAgent.ToString(),
            DeviceFp = Clean(req.Headers["X-Device-Fp"].ToString()),
            SessionId = Clean(req.Headers["X-Session-Id"].ToString()),
            Webdriver = req.Headers["X-Webdriver"].ToString() == "1",
            HadInput = req.Headers["X-Had-Input"].ToString() switch { "1" => true, "0" => false, _ => (bool?)null }
        };
    }

    private static string? Clean(string v)
    {
        v = (v ?? "").Trim();
        return v.Length == 0 ? null : (v.Length > 128 ? v[..128] : v);
    }

    /// <summary>
    /// Ưu tiên X-Forwarded-For (do Gateway thêm); nếu không có thì RemoteIpAddress.
    /// Trả null nếu không parse được (AdminAPI từ chối IP sai định dạng bằng 400).
    /// </summary>
    public static string? ResolveIp(HttpContext http)
    {
        string? raw = null;
        var xff = http.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(xff)) raw = xff.Split(',')[0].Trim();
        raw ??= http.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(raw) || !IPAddress.TryParse(raw, out var ip)) return null;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return "127.0.0.1";
        return ip.ToString();
    }
}
