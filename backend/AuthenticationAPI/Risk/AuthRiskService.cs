namespace AuthenticationAPI.Risk;

public interface IAuthRiskService
{
    /// <summary>Gọi đầu action Login: chặn IP/thiết bị/session đang bị block + ghi nhận rule bot.</summary>
    Task<RiskVerdict> CheckLoginAsync(HttpContext http, CancellationToken ct = default);

    /// <summary>Gọi đầu action Register: BOT_S02, FRD_A02 + rule bot.</summary>
    Task<RiskVerdict> CheckRegisterAsync(HttpContext http, string? email, CancellationToken ct = default);
}

/// <summary>
/// Rule ở AuthenticationAPI: BOT_S02, FRD_A02, và rule bot theo header (BOT_H*, BOT_E01, BOT_V01).
/// Chưa làm (cần GeoIP + bảng lịch sử đăng nhập): FRD_G01, FRD_G02, TRU_02.
/// FRD_A03 (đổi mật khẩu rồi đặt vé) do AdminAPI tự tính từ audit log - xem AdminAPI/patches.
/// </summary>
public sealed class AuthRiskService : IAuthRiskService
{
    private const int RegistrationsPerIp = 5;                       // BOT_S02: >= 5 tài khoản đăng ký từ 1 IP / 1 giờ
    private static readonly TimeSpan RegistrationWindow = TimeSpan.FromHours(1);

    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "10minutemail.com", "tempmail.com", "temp-mail.org",
        "yopmail.com", "trashmail.com", "sharklasers.com", "getnada.com", "dispostable.com",
        "throwawaymail.com", "maildrop.cc", "fakeinbox.com", "mailnesia.com", "mohmal.com"
    };

    private readonly IRiskGuard _guard;
    private readonly SlidingCounter _counters;
    private readonly ILogger<AuthRiskService> _logger;

    public AuthRiskService(IRiskGuard guard, SlidingCounter counters, ILogger<AuthRiskService> logger)
    { _guard = guard; _counters = counters; _logger = logger; }

    public async Task<RiskVerdict> CheckLoginAsync(HttpContext http, CancellationToken ct = default)
    {
        // Chưa biết userId lúc đăng nhập => chỉ kiểm tra theo IP / thiết bị / session.
        // Block ACCOUNT sẽ có hiệu lực khi người đó đặt vé (TicketAPI kiểm tra theo userId).
        var pre = await _guard.PreCheckAsync(http, userId: null, ct);
        if (!pre.Allowed) return pre;

        return await _guard.EvaluateAsync(http, new RiskEvent(), isBooking: false, ct);
    }

    public async Task<RiskVerdict> CheckRegisterAsync(HttpContext http, string? email, CancellationToken ct = default)
    {
        var pre = await _guard.PreCheckAsync(http, userId: null, ct);
        if (!pre.Allowed) return pre;

        var evt = new RiskEvent();
        var ip = RiskRequestContext.ResolveIp(http);
        try
        {
            if (ip != null)
            {
                var n = _counters.Hit($"s02:{ip}", RegistrationWindow);
                if (n >= RegistrationsPerIp)
                    evt.BotRules.Add(new RuleHit("BOT_S02", 1, $"{n} registrations from {ip} in 1h"));
            }

            var why = SuspiciousEmail(email);
            if (why != null) evt.FraudRules.Add(new RuleHit("FRD_A02", 1, why));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Risk rule calculation failed for register (ignored)"); }

        return await _guard.EvaluateAsync(http, evt, isBooking: false, ct);
    }

    /// <summary>FRD_A02: email dùng một lần, hoặc phần trước @ trông như sinh ngẫu nhiên.</summary>
    private static string? SuspiciousEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return null;
        var at = email.LastIndexOf('@');
        var local = email[..at].Trim();
        var domain = email[(at + 1)..].Trim();

        if (DisposableDomains.Contains(domain)) return $"disposable email domain {domain}";

        if (local.Length >= 12 && local.All(char.IsLetterOrDigit))
        {
            var digits = local.Count(char.IsDigit);
            var vowels = local.Count(ch => "aeiouAEIOU".Contains(ch));
            if (digits >= local.Length / 2 || vowels == 0) return $"random-looking email '{local}'";
        }
        return null;
    }
}
