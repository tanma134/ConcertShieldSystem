using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace TicketAPI.Risk;

public interface IChallengeVerifier
{
    // True when the caller already passed the challenge required by the risk decision.
    Task<bool> IsPassedAsync(HttpContext http, string challengeType, int? userId, CancellationToken ct = default);
}

// CAPTCHA: Cloudflare Turnstile token (header X-Challenge-Token), remembered ~10 minutes per session/device/IP.
// OTP: short-lived step-up JWT issued by AuthenticationAPI (header X-StepUp-Token), bound to the user.
public sealed class ChallengeVerifier : IChallengeVerifier
{
    public const string StepUpAudience = "AuthenticationAPI.StepUp";
    public const string StepUpPurpose = "stepup";
    private const string SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private static readonly TimeSpan PassTtl = TimeSpan.FromMinutes(10);

    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _cfg;
    private readonly ILogger<ChallengeVerifier> _logger;

    public ChallengeVerifier(IHttpClientFactory http, IMemoryCache cache, IConfiguration cfg, ILogger<ChallengeVerifier> logger)
    { _http = http; _cache = cache; _cfg = cfg; _logger = logger; }

    public async Task<bool> IsPassedAsync(HttpContext http, string challengeType, int? userId, CancellationToken ct = default)
    {
        if (string.Equals(challengeType, "OTP", StringComparison.OrdinalIgnoreCase))
            return ValidateStepUp(http, userId);
        return await CheckCaptchaAsync(http, ct);
    }

    private async Task<bool> CheckCaptchaAsync(HttpContext http, CancellationToken ct)
    {
        var c = RiskRequestContext.From(http);
        var who = c.SessionId ?? c.DeviceFp ?? c.Ip;
        if (who == null) return false;
        var cacheKey = $"chal:captcha:{who}";
        if (_cache.TryGetValue(cacheKey, out _)) return true;

        var token = http.Request.Headers["X-Challenge-Token"].ToString();
        if (string.IsNullOrWhiteSpace(token)) return false;

        var turnstileKey = _cfg["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(turnstileKey))
        {
            _logger.LogWarning("Turnstile:SecretKey is not configured; CAPTCHA challenge cannot be verified.");
            return false;
        }

        try
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new("secret", turnstileKey),
                new("response", token)
            };
            if (c.Ip != null) form.Add(new("remoteip", c.Ip));

            using var resp = await _http.CreateClient().PostAsync(SiteVerifyUrl, new FormUrlEncodedContent(form), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var ok = doc.RootElement.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (ok) _cache.Set(cacheKey, true, PassTtl);
            else _logger.LogInformation("Turnstile rejected token: {Body}", json);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Turnstile verification failed");
            return false;
        }
    }

    private bool ValidateStepUp(HttpContext http, int? userId)
    {
        if (userId == null) return false;
        var token = http.Request.Headers["X-StepUp-Token"].ToString();
        var signingKey = _cfg["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signingKey)) return false;

        try
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _cfg["JwtSettings:Issuer"] ?? "AuthenticationAPI",
                ValidateAudience = true,
                ValidAudience = StepUpAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            return principal.FindFirst("purpose")?.Value == StepUpPurpose
                && principal.FindFirst("sub")?.Value == userId.Value.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Invalid step-up token");
            return false;
        }
    }
}
