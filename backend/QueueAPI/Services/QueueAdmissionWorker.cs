using QueueAPI.Repositories;
using QueueAPI.Time;

namespace QueueAPI.Services;

public sealed class QueueAdmissionWorker(IServiceScopeFactory scopeFactory, ILogger<QueueAdmissionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IQueueRepository>();
                var redis = scope.ServiceProvider.GetRequiredService<IRedisQueueStore>();
                var now = QueueTimeZone.Now;
                // timestamp without time zone values are Vietnam local wall-clock values.
                var configs = await repository.GetConfigurationsEndedBeforeAsync(now, stoppingToken);
                foreach (var config in configs)
                {
                    var cycleKey = config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
                    if (config.QueueEnabled)
                    {
                        await redis.FinalizeRandomizationAsync(config.EventId, cycleKey, stoppingToken);
                        var admitted = await redis.AdmitNextAsync(config.EventId, config.AdmissionRate, stoppingToken);
                        if (admitted > 0) logger.LogInformation("Queue admitted {Count} users for eventId={EventId}", admitted, config.EventId);
                        await SyncQueueSessionStatusesAsync(repository, redis, config.EventId, stoppingToken);
                    }

                    var admittedSessions = await repository.GetAdmittedSessionsAsync(config.EventId, stoppingToken);
                    foreach (var session in admittedSessions)
                    {
                        if (!await redis.HasAdmissionAsync(config.EventId, session.UserId, stoppingToken))
                        {
                            await repository.ExpireAdmittedSessionAsync(
                                session.QueueSessionId,
                                QueueTimeZone.Now,
                                stoppingToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Queue admission cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task SyncQueueSessionStatusesAsync(
        IQueueRepository repository,
        IRedisQueueStore redis,
        int eventId,
        CancellationToken cancellationToken)
    {
        var sessions = await repository.GetSyncableSessionsAsync(eventId, cancellationToken);

        var changed = 0;
        foreach (var session in sessions)
        {
            var result = await redis.GetStatusAsync(eventId, session.UserId, session.QueueToken, cancellationToken);
            if (result.Status == "TOKEN_INVALID")
            {
                logger.LogWarning(
                    "Queue session could not be synchronized because its Redis token is invalid eventId={EventId} userId={UserId}",
                    eventId, session.UserId);
                continue;
            }

            if (result.Status is not ("WAITING" or "ADMITTED"))
                continue;

            var position = result.Status == "WAITING" && result.Position > 0
                ? result.Position
                : (int?)null;
            if (session.Status == result.Status && session.Position == position)
                continue;

            session.Status = result.Status;
            session.Position = position;
            session.UpdatedAt = QueueTimeZone.Now;
            changed++;
        }

        if (changed > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Synchronized {Count} queue session statuses from Redis for eventId={EventId}",
                changed, eventId);
        }
    }

}