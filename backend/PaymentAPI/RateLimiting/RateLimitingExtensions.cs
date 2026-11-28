using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Microsoft.Extensions.DependencyInjection;

public static class PaymentRateLimitPolicies
{
    public const string Payment = "payment";
}

public static class RateLimitingExtensions
{
    private static string PartitionKey(HttpContext http)
    {
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId)) return "u:" + userId;
        return "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

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

            // Tao link thanh toan / reconcile: 10 request / phut / user
            options.AddPolicy(PaymentRateLimitPolicies.Payment, http =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromSeconds(60),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        return services;
    }
}
