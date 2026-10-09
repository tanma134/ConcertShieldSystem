using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;
namespace TicketAPI.Services;
public class TicketChangeNotificationWorker(IServiceScopeFactory scopes, ILogger<TicketChangeNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var row = await db.Set<AffectedTicket>().FromSqlRaw("SELECT * FROM affected_tickets WHERE notified_at IS NULL ORDER BY id LIMIT 1 FOR UPDATE SKIP LOCKED").FirstOrDefaultAsync(ct);
                if (row != null)
                {
                    var change = await db.Set<AppliedEventChange>().AsNoTracking().SingleAsync(x => x.ChangeId == row.ChangeId, ct);
                    var client = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("ChangeNotification");
                    using var message = new HttpRequestMessage(HttpMethod.Post, "api/internal/notifications");
                    message.Headers.Add("Idempotency-Key", $"change-{row.ChangeId}-ticket-{row.TicketId}");
                    message.Content = JsonContent.Create(new { userId = row.CustomerId, title = change.Title + ": " + change.Type, message = change.Reason + ". Your ticket remains valid. See My Tickets for the current schedule and return policy.", category = "event_change", targetUrl = "/my-tickets" });
                    using var response = await client.SendAsync(message, ct); response.EnsureSuccessStatusCode();
                    row.NotifiedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogWarning(ex, "Change notification pending; retrying"); }
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }
    }
}
