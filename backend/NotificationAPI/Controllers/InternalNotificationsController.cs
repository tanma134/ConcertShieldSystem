using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationAPI.Data;
using NotificationAPI.DTOs;
using NotificationAPI.Models;
namespace NotificationAPI.Controllers;
[ApiController]
[Route("api/internal/notifications")]
public class InternalNotificationsController(NotificationDbContext db, IConfiguration config) : ControllerBase
{
    // Advisory lock + delivery key bảo đảm retry không tạo hai thông báo sau timeout.
    [HttpPost]
    public async Task<IActionResult> Create(CreateNotificationDTO input, CancellationToken ct)
    {
        var expected = config["Governance:InternalApiKey"];
        if (string.IsNullOrEmpty(expected) || Request.Headers["X-Internal-Api-Key"] != expected) return Unauthorized();
        var key = Request.Headers["Idempotency-Key"].ToString();
        if (key.Length == 0 || key.Length > 200 || input.UserId <= 0 || string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 255 || string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 4000 || input.Category.Length > 50 || input.TargetUrl?.Length > 500)
            return BadRequest(new { message = "Invalid notification." });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        if (await db.Notifications.AnyAsync(x => x.DeliveryKey == key, ct)) return Ok(new { success = true });
        db.Notifications.Add(new Notification { UserId = input.UserId, Title = input.Title, Message = input.Message, Category = input.Category, TargetUrl = input.TargetUrl, DeliveryKey = key });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(new { success = true });
    }
}
