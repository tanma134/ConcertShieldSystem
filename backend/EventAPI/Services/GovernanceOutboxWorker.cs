using System.Text.Json;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
namespace EventAPI.Services;
public class GovernanceOutboxWorker(IServiceScopeFactory scopes, ILogger<GovernanceOutboxWorker> logger) : BackgroundService
{
    // SKIP LOCKED cho nhiều instance; giữ job trong transaction đến khi có phản hồi từ service đích.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EventDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                var job = await db.Set<GovernanceOutbox>().FromSqlRaw("SELECT * FROM governance_outbox WHERE completed_at IS NULL AND next_attempt_at <= now() ORDER BY id LIMIT 1 FOR UPDATE SKIP LOCKED").FirstOrDefaultAsync(stoppingToken);
                if (job != null)
                {
                    try
                    {
                        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                        if (job.Kind == "DeleteAsset")
                        {
                            var publicId = JsonSerializer.Deserialize<string>(job.Payload)!;
                            if (!await db.Set<ComplianceDocument>().AnyAsync(x => x.PublicId == publicId, stoppingToken))
                                await scope.ServiceProvider.GetRequiredService<IComplianceAssetStore>().DeleteAsync(publicId, stoppingToken);
                        }
                        else
                        {
                            var client = factory.CreateClient(job.Kind == "ApplyChange" ? "GovernanceTicket" : "GovernanceNotification");
                            using var message = new HttpRequestMessage(HttpMethod.Post, job.Kind == "ApplyChange" ? "api/internal/event-changes/apply" : "api/internal/notifications");
                            message.Headers.Add("Idempotency-Key", "event-outbox-" + job.Id);
                            message.Content = new StringContent(job.Payload, System.Text.Encoding.UTF8, "application/json");
                            using var response = await client.SendAsync(message, stoppingToken); response.EnsureSuccessStatusCode();
                            if (job.Kind == "ApplyChange")
                            {
                                var change = JsonSerializer.Deserialize<ApplyChangeMessage>(job.Payload)!;
                                var request = await db.Set<EventChangeRequest>().SingleAsync(x => x.Id == change.ChangeId, stoppingToken);
                                request.ProcessingStatus = "Completed";
                                // Cùng thứ tự khóa với API: job -> event; API không khóa các job hiện có.
                                var ev = await db.Events.FromSqlInterpolated($"SELECT *, xmin FROM events WHERE event_id = {change.EventId} FOR UPDATE").SingleAsync(stoppingToken);
                                if (ev.ScheduleVersion == change.ScheduleVersion && ev.Status == "Published") ev.SalesFrozen = false;
                            }
                        }
                        job.CompletedAt = DateTime.UtcNow; job.LastError = null;
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        job.Attempts++; job.LastError = "Delivery failed; see server logs.";
                        job.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(job.Attempts, 6))));
                        logger.LogWarning(ex, "Outbox delivery failed for {Id}", job.Id);
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
                await tx.CommitAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Outbox worker iteration failed"); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
