using Microsoft.AspNetCore.Mvc;

namespace TicketAPI.Risk;

public sealed record RiskVerdict(bool Allowed, int StatusCode, string? Code, string? Message, RiskResult? Result)
{
    public static RiskVerdict Allow(RiskResult? r = null) => new(true, 200, null, null, r);

    // Response body shape expected by frontend: { success: false, code, message }.
    public IActionResult ToActionResult() =>
        new ObjectResult(new { success = false, code = Code, message = Message, challengeType = Result?.ChallengeType })
        { StatusCode = StatusCode };
}

public interface IRiskGuard
{
    Task<RiskVerdict> PreCheckAsync(HttpContext http, int? userId, CancellationToken ct = default);

    RiskVerdict CheckEkycAsync(HttpContext http, string? action = null);

    // Evaluates bot rules and sends matched rules to AdminAPI; skips clean requests with zero matches.
    Task<RiskVerdict> EvaluateAsync(HttpContext http, RiskEvent evt, bool isBooking, CancellationToken ct = default);
}

public sealed class RiskGuard : IRiskGuard
{
    private readonly IRiskClient _client;
    private readonly SlidingCounter _counters;
    private readonly IConfiguration _cfg;
    private readonly ILogger<RiskGuard> _logger;
    private readonly IChallengeVerifier? _challenges;

    public RiskGuard(IRiskClient client, SlidingCounter counters, IConfiguration cfg, ILogger<RiskGuard> logger,
        IChallengeVerifier? challenges = null)
    { _client = client; _counters = counters; _cfg = cfg; _logger = logger; _challenges = challenges; }

    private bool Enabled => _cfg.GetValue("Risk:Enabled", true);

    // Customer must pass eKYC before buying (read from login JWT claim; toggleable via Risk:RequireEkycForPurchase).
    public RiskVerdict CheckEkycAsync(HttpContext http, string? action = null)
    {
        if (!_cfg.GetValue("Risk:RequireEkycForPurchase", true)) return RiskVerdict.Allow();

        // Staff/Admin/Organizer flows (door scanning, organizer tools) are not purchases.
        if (http.User.IsInRole("Admin") || http.User.IsInRole("Staff") || http.User.IsInRole("Organizer"))
            return RiskVerdict.Allow();

        var status = http.User.FindFirst("ekyc_status")?.Value;
        if (string.Equals(status, "Passed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Verified", StringComparison.OrdinalIgnoreCase))
            return RiskVerdict.Allow();

        return new RiskVerdict(false, 403, "EKYC_REQUIRED",
            "Please complete identity verification (eKYC) before booking tickets.", null);
    }

    public async Task<RiskVerdict> PreCheckAsync(HttpContext http, int? userId, CancellationToken ct = default)
    {
        if (!Enabled) return RiskVerdict.Allow();
        var c = RiskRequestContext.From(http);
        var r = await _client.CheckBlockAsync(userId, c.Ip, c.DeviceFp, c.SessionId, ct);
        if (r is { Blocked: true })
            return new RiskVerdict(false, 403, "RISK_BLOCKED",
                r.Message ?? "Your account or device is temporarily restricted due to unusual activity.", null);
        return RiskVerdict.Allow();
    }

    public async Task<RiskVerdict> EvaluateAsync(HttpContext http, RiskEvent evt, bool isBooking, CancellationToken ct = default)
    {
        if (!Enabled) return RiskVerdict.Allow();

        var c = RiskRequestContext.From(http);
        var det = BotRuleDetector.Detect(c, _counters, _cfg, isBooking);

        evt.SessionId ??= c.SessionId;
        evt.DeviceFp ??= c.DeviceFp;
        evt.IpAddress ??= c.Ip;
        evt.BotRules.AddRange(det.Hits);
        evt.SignalsMissing.AddRange(det.Missing);

        // Skip clean requests with no matched rules.
        if (evt.BotRules.Count == 0 && evt.FraudRules.Count == 0) return RiskVerdict.Allow();

        // AdminAPI requires at least one of UserId, SessionId, DeviceFp, or IpAddress.
        if (evt.UserId == null && evt.SessionId == null && evt.DeviceFp == null && evt.IpAddress == null)
            return RiskVerdict.Allow();

        var res = await _client.IngestAsync(evt, ct);
        if (res == null) return RiskVerdict.Allow();   // fail-open

        _logger.LogInformation("Risk decision: {Action}/{Level} score={Score} shadow={Shadow} rules={Bot}+{Fraud}",
            res.Action, res.Level, res.Score, res.IsShadow,
            string.Join(",", evt.BotRules.Select(r => r.Code)), string.Join(",", evt.FraudRules.Select(r => r.Code)));

        // SHADOW: log only, do not enforce blocks.
        if (res.IsShadow) return RiskVerdict.Allow(res);

        // CHALLENGE already satisfied (Turnstile token / step-up token)? Let the request through.
        if (res.Action == "CHALLENGE" && _challenges != null &&
            await _challenges.IsPassedAsync(http, res.ChallengeType ?? "CAPTCHA", evt.UserId, ct))
            return RiskVerdict.Allow(res);

        return res.Action switch
        {
            "BLOCK" => new RiskVerdict(false, 403, "RISK_BLOCKED",
                "Your account or device is temporarily restricted due to unusual activity. Please contact support if you think this is a mistake.", res),
            "HOLD" => new RiskVerdict(false, 403, "RISK_HOLD",
                "This action is being reviewed by our team. Please try again later.", res),
            "CHALLENGE" => new RiskVerdict(false, 428, "RISK_CHALLENGE",
                $"Additional verification required ({res.ChallengeType ?? "OTP/CAPTCHA"}).", res),
            _ => RiskVerdict.Allow(res)
        };
    }
}
