using AdminAPI.Models;

namespace AdminAPI.Services.Risks;

internal static class RiskSupport
{
    public static string? MaskIp(string? ip)
    {
        if (string.IsNullOrEmpty(ip)) return null;
        if (ip.Contains('.')) { var p = ip.Split('.'); return p.Length == 4 ? $"{p[0]}.{p[1]}.{p[2]}.x" : "masked"; }
        var g = ip.Split(':');
        return g.Length >= 3 ? $"{g[0]}:{g[1]}:{g[2]}::x" : "masked";
    }

    public static string? MaskToken(string? s) =>
        string.IsNullOrEmpty(s) ? null : s.Length > 8 ? s[..8] + "…" : s;

    private static string Subject(string? scope) => scope?.ToUpperInvariant() switch
    {
        "TICKET" => "ticket",
        "IP" => "network connection",
        "DEVICE" => "device",
        "SESSION" => "session",
        _ => "account"
    };

    // Where the customer should land when they open the notification.
    public static string? TargetUrl(string? type, string? scope) =>
        type switch
        {
            "BLOCK" when string.Equals(scope, "TICKET", StringComparison.OrdinalIgnoreCase) => "/my-tickets",
            "BLOCK" => "/my-profile",
            "RESTORE" when string.Equals(scope, "TICKET", StringComparison.OrdinalIgnoreCase) => "/my-tickets",
            "RESTORE" => "/my-profile",
            "APPEAL_ACCEPTED" or "APPEAL_REJECTED" => "/my-profile",
            _ => null
        };

    public static RiskNotification BlockNotice(RiskDecision d, RiskConfig cfg, DateTime now) => new()
    {
        UserId = d.UserId!.Value,
        RiskDecision = d,
        Type = "BLOCK",
        Scope = d.Scope,
        Title = $"Activity on your {Subject(d.Scope)} has been temporarily blocked",
        Message = (d.ReasonPublic ?? "Unusual activity was detected.")
                  + (d.Scope == "TICKET"
                        ? $" This ticket is blocked and cannot be used for entry. If you believe this is a mistake, you can submit an appeal within {cfg.AppealWindowText(d.Scope)} of this notice."
                        : $" If you believe this is a mistake, you have the right to submit an appeal within {cfg.AppealWindowText(d.Scope)} of this notice.")
                  + (d.ExpiresAt is DateTime e ? $" The temporary block is in effect until {e:yyyy-MM-dd HH:mm} UTC." : ""),
        CanAppeal = true,
        AppealDeadlineAt = cfg.AppealDueAt(d.Scope, now),
        CreatedAt = now
    };

    public static RiskNotification RestoreNotice(RiskDecision d, DateTime now) => new()
    {
        UserId = d.UserId!.Value,
        RiskDecision = d,
        Type = "RESTORE",
        Scope = d.Scope,
        Title = $"The block on your {Subject(d.Scope)} has been lifted",
        Message = $"The restriction on your {Subject(d.Scope)} has been lifted. You can use it as normal.",
        CanAppeal = false,
        CreatedAt = now
    };

    public static RiskNotification ExpiryNotice(RiskDecision d, DateTime now) => new()
    {
        UserId = d.UserId!.Value,
        RiskDecision = d,
        Type = "EXPIRY",
        Scope = d.Scope,
        Title = $"The temporary block on your {Subject(d.Scope)} has ended",
        Message = $"The temporary block on your {Subject(d.Scope)} has ended automatically. You can use it as normal.",
        CanAppeal = false,
        CreatedAt = now
    };

    public static RiskNotification AppealNotice(RiskAppeal a, RiskDecision d, bool accepted, DateTime now) => new()
    {
        UserId = a.UserId,
        RiskDecision = d,
        RiskAppealId = a.RiskAppealId,
        Type = accepted ? "APPEAL_ACCEPTED" : "APPEAL_REJECTED",
        Scope = d.Scope,
        Title = accepted ? "Your appeal has been accepted" : "Your appeal has been rejected",
        Message = accepted
            ? $"After review, the restriction on your {Subject(d.Scope)} has been lifted."
            : string.Equals(d.Scope, "TICKET", StringComparison.OrdinalIgnoreCase)
                ? "After review, the original decision was kept. The ticket has been withdrawn and your refund is being processed according to the event refund policy."
                : "After review, the original decision was kept because the risk indicators still apply.",
        CanAppeal = false,
        CreatedAt = now
    };
}