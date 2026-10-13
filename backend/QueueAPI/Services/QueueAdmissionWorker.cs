using Microsoft.EntityFrameworkCore;
using QueueAPI.Data;
using QueueAPI.Time;

namespace QueueAPI.Services;

public sealed class QueueAdmissionWorker(IServiceScopeFactory scopeFactory, ILogger<QueueAdmissionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan AbuseWindow = TimeSpan.FromMinutes(5);
    private const int MaxUserSessionsInWindow = 2;
    private const int MaxEventSessionsInWindow = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<QueueDbContext>();
                var redis = scope.ServiceProvider.GetRequiredService<IRedisQueueStore>();
                var now = QueueTimeZone.Now;
                await ScanRegistrationAbuseAsync(db, now, stoppingToken);
                // timestamp without time zone values are Vietnam local wall-clock values.
                var configs = await db.QueueConfigurations.AsNoTracking()
                    .Where(x => x.PreQueueEndTime <= now)
                    .ToListAsync(stoppingToken);
                foreach (var config in configs)
                {
                    var cycleKey = config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
                    if (config.QueueEnabled)
                    {
                        await redis.FinalizeRandomizationAsync(config.EventId, cycleKey, stoppingToken);
                        var admitted = await redis.AdmitNextAsync(config.EventId, config.AdmissionRate, stoppingToken);
                        if (admitted > 0) logger.LogInformation("Queue admitted {Count} users for eventId={EventId}", admitted, config.EventId);
                    }

                    var admittedSessions = await db.QueueSessions
                        .Where(x => x.EventId == config.EventId && x.Status == "ADMITTED")
                        .Select(x => new { x.QueueSessionId, x.UserId })
                        .ToListAsync(stoppingToken);
                    foreach (var session in admittedSessions)
                    {
                        if (!await redis.HasAdmissionAsync(config.EventId, session.UserId, stoppingToken))
                        {
                            await db.QueueSessions
                                .Where(x => x.QueueSessionId == session.QueueSessionId && x.Status == "ADMITTED")
                                .ExecuteUpdateAsync(update => update
                                    .SetProperty(x => x.Status, "EXPIRED")
                                    .SetProperty(x => x.UpdatedAt, QueueTimeZone.Now), stoppingToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Queue admission cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ScanRegistrationAbuseAsync(QueueDbContext db, DateTime now, CancellationToken cancellationToken)
    {
        var windowStart = now - AbuseWindow;
        var sessions = await db.QueueSessions.AsNoTracking()
            .Where(x => x.CreatedAt >= windowStart &&
                x.Status != "LEFT" && x.Status != "EXPIRED" &&
                x.Status != "REVOKED" && x.Status != "BLOCKED")
            .ToListAsync(cancellationToken);

        var userGroups = sessions.GroupBy(x => x.UserId).Where(x => x.Count() > MaxUserSessionsInWindow);
        foreach (var group in userGroups)
        {
            var current = group.OrderByDescending(x => x.CreatedAt).First();
            await CreateAlertIfMissingAsync(db, current, "MULTIPLE_USER_SESSIONS",
                $"RULE: User created {group.Count()} queue sessions within 5 minutes (limit: {MaxUserSessionsInWindow}).",
                windowStart, cancellationToken);
        }

        var eventGroups = sessions.GroupBy(x => x.EventId).Where(x => x.Count() > MaxEventSessionsInWindow);
        foreach (var group in eventGroups)
        {
            var current = group.OrderByDescending(x => x.CreatedAt).First();
            await CreateAlertIfMissingAsync(db, current, "EVENT_SESSION_SPIKE",
                $"RULE: Event received {group.Count()} queue sessions within 5 minutes (limit: {MaxEventSessionsInWindow}).",
                windowStart, cancellationToken);
        }
    }

    private async Task CreateAlertIfMissingAsync(
        QueueDbContext db,
        QueueAPI.Models.QueueSession session,
        string alertType,
        string behavior,
        DateTime windowStart,
        CancellationToken cancellationToken)
    {
        var exists = await db.AbuseAlerts.AnyAsync(x =>
            x.SessionId == session.SessionId &&
            x.AlertType == alertType &&
            x.CreatedAt >= windowStart, cancellationToken);
        if (exists) return;

        db.AbuseAlerts.Add(new QueueAPI.Models.AbuseAlert
        {
            EventId = session.EventId,
            UserId = session.UserId,
            SessionId = session.SessionId,
            QueueToken = session.QueueToken,
            AlertType = alertType,
            Severity = "HIGH",
            Status = "OPEN",
            DetectedBehavior = behavior,
            CreatedAt = QueueTimeZone.Now
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning(
            "Automatic queue abuse alert created type={AlertType} eventId={EventId} userId={UserId} sessionId={SessionId}",
            alertType, session.EventId, session.UserId, session.SessionId);
    }
}