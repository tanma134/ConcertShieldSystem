namespace AuthenticationAPI.Risk;

public sealed record BotDetection(List<RuleHit> Hits, List<string> Missing);

/// <summary>
/// Phát hiện rule Bot từ request (nhóm H, E, V). Chỉ dùng cho endpoint do TRÌNH DUYỆT gọi
/// (đăng nhập, giữ vé, tạo đơn) - KHÔNG dùng cho lời gọi giữa các service.
/// Ngưỡng nằm ngay đầu file để khớp với bảng tham số ở AdminAPI.
/// </summary>
public static class BotRuleDetector
{
    // BOT_V01: req/s trong cửa sổ 10 giây. Mức 1: > 5 req/s, mức 2: > 10 req/s  => > 50 / > 100 request trong 10s.
    private const int V01_Level1_Count = 50;
    private const int V01_Level2_Count = 100;
    private static readonly TimeSpan V01_Window = TimeSpan.FromSeconds(10);
    // BOT_V04: > 30 lượt đặt vé / phút / IP
    private const int V04_Count = 30;
    private static readonly TimeSpan V04_Window = TimeSpan.FromSeconds(60);

    // Danh sách UA của BOT_H01. Khớp với DB: có thêm okhttp, httpclient, postman (mục 12 tài liệu).
    private static readonly string[] CliAgents =
    {
        "curl", "wget", "python-requests", "python-urllib", "go-http-client", "axios", "okhttp",
        "httpclient", "postman", "java/", "libwww-perl", "node-fetch", "insomnia"
    };
    private static readonly string[] HeadlessAgents =
        { "headlesschrome", "phantomjs", "puppeteer", "playwright", "selenium", "webdriver" };

    /// <param name="isBooking">true cho giữ vé/tạo đơn: bật thêm V04 và E03.</param>
    public static BotDetection Detect(RiskRequestContext c, SlidingCounter counters, IConfiguration cfg, bool isBooking)
    {
        var hits = new List<RuleHit>();
        var missing = new List<string>();
        var h = c.Request.Headers;
        var ua = c.UserAgent ?? "";
        var who = c.SessionId ?? c.Ip ?? "unknown";

        // ---- H: Header ----
        if (string.IsNullOrWhiteSpace(ua))
            hits.Add(new RuleHit("BOT_H01", 1, "(empty user-agent)"));
        else if (CliAgents.FirstOrDefault(a => ua.Contains(a, StringComparison.OrdinalIgnoreCase)) is { } cli)
            hits.Add(new RuleHit("BOT_H01", 1, Trim(ua)));

        var claimsBrowser = ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase) || ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase);
        if (claimsBrowser && (!h.ContainsKey("sec-ch-ua") || !h.ContainsKey("sec-fetch-site")))
            hits.Add(new RuleHit("BOT_H02", 1, "browser UA without sec-ch-ua/sec-fetch-*"));

        if (!h.ContainsKey("Accept-Language") || !h.ContainsKey("Accept-Encoding"))
            hits.Add(new RuleHit("BOT_H03", 1, "missing Accept-Language/Accept-Encoding"));

        var allowed = cfg.GetSection("Risk:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173", "https://localhost:5173" };
        var origin = h["Origin"].ToString();
        if (string.IsNullOrEmpty(origin)) origin = h["Referer"].ToString();
        if (string.IsNullOrEmpty(origin) || !allowed.Any(a => origin.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
            hits.Add(new RuleHit("BOT_H04", 1, string.IsNullOrEmpty(origin) ? "(no origin/referer)" : Trim(origin)));
        // BOT_H05 (gọi API đặt vé khi không có token) không cần ở đây: endpoint [Authorize] đã trả 401 trước khi tới.

        // ---- E: Môi trường ----
        if (c.Webdriver || HeadlessAgents.Any(a => ua.Contains(a, StringComparison.OrdinalIgnoreCase)))
            hits.Add(new RuleHit("BOT_E01", 1, c.Webdriver ? "navigator.webdriver = true" : Trim(ua)));
        // BOT_E02 (datacenter/VPN/Tor) cần dịch vụ tra ASN - chưa làm.

        if (isBooking)
        {
            if (c.HadInput == false) hits.Add(new RuleHit("BOT_E03", 1, "no mouse/touch/scroll before submit"));
            else if (c.HadInput == null) missing.Add("had_input");
        }

        // ---- V: Tốc độ ----
        var perSession = counters.Hit($"v01:{who}", V01_Window);
        if (perSession > V01_Level2_Count) hits.Add(new RuleHit("BOT_V01", 2, $"{perSession} requests/10s"));
        else if (perSession > V01_Level1_Count) hits.Add(new RuleHit("BOT_V01", 1, $"{perSession} requests/10s"));

        if (isBooking && c.Ip != null)
        {
            var perIp = counters.Hit($"v04:{c.Ip}", V04_Window);
            if (perIp > V04_Count) hits.Add(new RuleHit("BOT_V04", 1, $"{perIp} bookings/min from {c.Ip}"));
        }

        if (c.DeviceFp == null) missing.Add("device_fp");
        return new BotDetection(hits, missing);
    }

    private static string Trim(string s) => s.Length > 80 ? s[..80] : s;
}
