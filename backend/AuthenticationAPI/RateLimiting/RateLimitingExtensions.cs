using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Microsoft.Extensions.DependencyInjection;

public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Otp = "auth-otp";
}

public static class RateLimitingExtensions
{
    private static string ClientKey(HttpContext http) =>
        "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private static FixedWindowRateLimiterOptions Window(int permits, int seconds) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromSeconds(seconds),
        QueueLimit = 0,
        AutoReplenishment = true
    };

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, ct) =>
            {
                var response = ctx.HttpContext.Response;
                var retryAfterSeconds = 60;
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                response.ContentType = "application/json";
                await response.WriteAsync(
                    $"{{\"message\":\"Too many requests. Please retry after {retryAfterSeconds} seconds.\"}}", ct);
            };

            // Dang nhap / dang ky / Google: 10 request / phut / IP
            options.AddPolicy(AuthRateLimitPolicies.Login, http =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ => Window(10, 60)));

            // OTP (quen mat khau, verify OTP, step-up): 5 request / 5 phut / IP
            options.AddPolicy(AuthRateLimitPolicies.Otp, http =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ => Window(5, 300)));
        });
        return services;
    }
}
