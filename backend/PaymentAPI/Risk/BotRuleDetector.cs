namespace PaymentAPI.Risk;

public sealed record BotDetection(List<RuleHit> Hits, List<string> Missing);

// Detects bot rules (H, E, V groups) for browser-facing endpoints only;
// thresholds match AdminAPI's parameter table.
public static class BotRuleDetector
{
    // BOT_V01: > 5 req/s (level 1) or > 10 req/s (level 2) in a 10s window.
    private const int V01_Level1_Count = 50;
    private const int V01_Level2_Count = 100;
    private static readonly TimeSpan V01_Window = TimeSpan.FromSeconds(10);
    // BOT_V04: > 30 booking requests/min per IP.
    private const int V04_Count = 30;
    private static readonly TimeSpan V04_Window = TimeSpan.FromSeconds(60);

    // BOT_H01 user-agent list aligned with AdminAPI policy table.
    private static readonly string[] CliAgents =
    {
        "curl", "wget", "python-requests", "python-urllib", "go-http-client", "axios", "okhttp",
        "httpclient", "postman", "java/", "libwww-perl", "node-fetch", "insomnia"
    };
    private static readonly string[] HeadlessAgents =
        { "headlesschrome", "phantomjs", "puppeteer", "playwright", "selenium", "webdriver" };

    public static BotDetection Detect(RiskRequestContext c, SlidingCounter counters, IConfiguration cfg, bool isBooking)
    {
        var hits = new List<RuleHit>();
        var missing = new List<string>();
        var h = c.Request.Headers;
        var ua = c.UserAgent ?? "";
        var who = c.SessionId ?? c.Ip ?? "unknown";

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
        // BOT_H05 (unauthenticated booking) is handled upstream by [Authorize] returning 401.

        if (c.Webdriver || HeadlessAgents.Any(a => ua.Contains(a, StringComparison.OrdinalIgnoreCase)))
            hits.Add(new RuleHit("BOT_E01", 1, c.Webdriver ? "navigator.webdriver = true" : Trim(ua)));
        // BOT_E02 (datacenter/VPN/Tor) requires an ASN lookup service (not yet implemented).

        if (isBooking)
        {
            if (c.HadInput == false) hits.Add(new RuleHit("BOT_E03", 1, "no mouse/touch/scroll before submit"));
            else if (c.HadInput == null) missing.Add("had_input");
        }

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
