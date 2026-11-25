using System.Net.Http.Json;
using AdminAPI.Models;
using AdminAPI.Repositories;

namespace AdminAPI.Services.Risks;

public sealed class RiskMaintenanceWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<RiskMaintenanceWorker> _logger;

    public RiskMaintenanceWorker(IServiceScopeFactory scopes, IHttpClientFactory http,
        ILogger<RiskMaintenanceWorker> logger)
    {
        _scopes = scopes;
        _http = http;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay so the app finishes booting before the first sweep.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireBlocksAsync(stoppingToken);
                await DeliverNotificationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Risk maintenance sweep failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    // Marks expired temporary blocks as Expired and lifts the ticket restriction.
    private async Task ExpireBlocksAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var decisions = scope.ServiceProvider.GetRequiredService<IRiskDecisionRepository>();
        var notifications = scope.ServiceProvider.GetRequiredService<IRiskNotificationRepository>();
        var tickets = scope.ServiceProvider.GetService<ITicketRiskClient>();
        var now = DateTime.UtcNow;

        var expired = await decisions.GetExpiredBlocksAsync(now, BatchSize, ct);

        if (expired.Count == 0) return;

        var failedTicketIds = new HashSet<int>();

        foreach (var group in expired.Where(d => d.Scope == "TICKET" && d.TicketId.HasValue)
                     .GroupBy(d => d.TicketId!.Value))
        {
            // Only lift the ticket if no other active ticket block remains.
            var stillBlocked = await decisions.HasUnexpiredTicketBlockAsync(group.Key, now, ct);
            if (stillBlocked || tickets == null) continue;

            try
            {
                await tickets.UnblockTicketAsync(group.Key,
                    "Temporary risk block expired automatically", 0, ct);
            }
            catch (Exception ex)
            {
                // Keep the decision Active so the next sweep retries the unblock.
                failedTicketIds.Add(group.Key);
                _logger.LogWarning(ex, "Could not lift expired ticket block for ticket {TicketId}", group.Key);
            }
        }

        foreach (var d in expired)
        {
            if (d.Scope == "TICKET" && d.TicketId.HasValue && failedTicketIds.Contains(d.TicketId.Value))
                continue;
            d.Status = "Expired";
        }

        foreach (var d in expired.Where(d => d.Status == "Expired" && d.UserId.HasValue))
            await notifications.AddAsync(RiskSupport.ExpiryNotice(d, now), ct);

        await decisions.SaveChangesAsync(ct);
        _logger.LogInformation("Expired {Count} risk block(s)", expired.Count);
    }

    // Pushes undelivered notifications to NotificationAPI.
    private async Task DeliverNotificationsAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<IRiskNotificationRepository>();
        var client = _http.CreateClient("RiskNotificationDelivery");

        var pending = await notifications.GetPendingAsync(BatchSize, ct);

        if (pending.Count == 0) return;

        foreach (var n in pending)
        {
            var payload = new
            {
                userId = n.UserId,
                title = n.Title,
                message = n.Message,
                category = "fraud_risk",
                targetUrl = RiskSupport.TargetUrl(n.Type, n.Scope)
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/internal/notifications")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("Idempotency-Key", $"risk-notification-{n.RiskNotificationId}");

            try
            {
                using var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Notification delivery for risk notification {Id} returned {Status}",
                        n.RiskNotificationId, (int)response.StatusCode);
                    continue;
                }
                n.DeliveredAt = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Notification delivery failed for risk notification {Id}",
                    n.RiskNotificationId);
                break; // NotificationAPI is probably down; retry on the next sweep.
            }
        }

        await notifications.SaveChangesAsync(ct);
    }
}
