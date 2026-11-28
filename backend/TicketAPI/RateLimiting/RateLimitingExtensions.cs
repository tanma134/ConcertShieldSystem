using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Microsoft.Extensions.DependencyInjection;

public static class TicketRateLimitPolicies
{
    public const string Hold = "hold";
    public const string Order = "order";
}

public static class RateLimitingExtensions
{
    private static string PartitionKey(HttpContext http)
    {
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId)) return "u:" + userId;
        return "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

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

            // Giu cho / huy giu / doi ghe: 30 request / phut / user
            options.AddPolicy(TicketRateLimitPolicies.Hold, http =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(http), _ => Window(30, 60)));

            // Tao don hang: 10 request / phut / user
            options.AddPolicy(TicketRateLimitPolicies.Order, http =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(http), _ => Window(10, 60)));
        });
        return services;
    }
}
