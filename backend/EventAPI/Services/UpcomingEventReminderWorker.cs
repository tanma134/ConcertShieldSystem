using System.Net.Http.Json;
using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Services;

public sealed class UpcomingEventReminderWorker(
    IServiceScopeFactory scopes,
    IHttpClientFactory httpClientFactory,
    ILogger<UpcomingEventReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EventDbContext>();
                await InsertUpcomingRemindersAsync(db, stoppingToken);
                while (await DeliverNextReminderAsync(db, stoppingToken))
                {
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Upcoming event reminder worker iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private static Task<int> InsertUpcomingRemindersAsync(EventDbContext db, CancellationToken ct)
        => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO public.event_reminder_deliveries
                (event_id, user_id, scheduled_start_at, created_at, next_attempt_at)
            SELECT e.event_id, w.user_id, e.starts_at, now(), now()
            FROM public.events e
            INNER JOIN public.wishlists w ON w.event_id = e.event_id
            WHERE e.status = 'Published'
              AND e.is_deleted = false
              AND e.starts_at > now()
              AND e.starts_at <= now() + interval '24 hours'
            ON CONFLICT (event_id, user_id, scheduled_start_at) DO NOTHING;
            """, ct);

    private async Task<bool> DeliverNextReminderAsync(EventDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var deliveries = await db.EventReminderDeliveries
            .FromSqlRaw("""
                SELECT d.*
                FROM public.event_reminder_deliveries d
                INNER JOIN public.events e
                    ON e.event_id = d.event_id
                   AND e.starts_at = d.scheduled_start_at
                WHERE d.sent_at IS NULL
                  AND d.next_attempt_at <= now()
                  AND e.status = 'Published'
                  AND e.is_deleted = false
                  AND e.starts_at > now()
                  AND e.starts_at <= now() + interval '24 hours'
                  AND EXISTS (
                      SELECT 1
                      FROM public.wishlists w
                      WHERE w.event_id = d.event_id AND w.user_id = d.user_id
                  )
                ORDER BY e.starts_at, d.event_id, d.user_id
                LIMIT 1
                FOR UPDATE OF d SKIP LOCKED
                """)
            .ToListAsync(ct);
        var delivery = deliveries.FirstOrDefault();

        if (delivery is null)
        {
            await transaction.CommitAsync(ct);
            return false;
        }

        var eventInfo = await db.Events
            .AsNoTracking()
            .Where(e => e.EventId == delivery.EventId && e.StartsAt == delivery.ScheduledStartAt)
            .Select(e => new { e.Title, e.Slug, e.StartsAt, e.Timezone })
            .SingleAsync(ct);

        try
        {
            var client = httpClientFactory.CreateClient("EventReminderEmail");
            using var response = await client.PostAsJsonAsync(
                "api/internal/event-reminders",
                new
                {
                    delivery.EventId,
                    delivery.UserId,
                    eventTitle = eventInfo.Title,
                    eventSlug = eventInfo.Slug,
                    startsAt = eventInfo.StartsAt,
                    timezone = eventInfo.Timezone
                },
                ct);
            response.EnsureSuccessStatusCode();

            delivery.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            delivery.AttemptCount++;
            var retrySeconds = Math.Min(300, 5 * Math.Pow(2, Math.Min(delivery.AttemptCount, 6)));
            delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(retrySeconds);
            logger.LogWarning(ex, "Failed sending event reminder for event {EventId} to user {UserId}", delivery.EventId, delivery.UserId);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
