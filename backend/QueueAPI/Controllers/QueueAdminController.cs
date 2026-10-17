using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueueAPI.Data;
using QueueAPI.DTOs;
using QueueAPI.Models;
using QueueAPI.Services;
using QueueAPI.Time;

namespace QueueAPI.Controllers;

[ApiController]
[Route("api/queue/admin")]
[Authorize(Policy = "QueueAdmin")]
public sealed class QueueAdminController(QueueDbContext db, IRedisQueueStore redis) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "0");

    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<QueueAdminSessionResponse>>> Sessions([FromQuery] int? eventId = null)
    {
        var query = db.QueueSessions.AsNoTracking().AsQueryable();
        if (eventId.HasValue) query = query.Where(x => x.EventId == eventId);
        var sessions = await query.OrderByDescending(x => x.UpdatedAt).Take(500).ToListAsync();
        return Ok(sessions.Select(x => new QueueAdminSessionResponse(x.QueueSessionId, x.EventId, x.UserId, x.SessionId, x.Status, x.Position, x.QueueToken[..8] + "...", QueueTimeZone.AsVietnamOffset(x.UpdatedAt))));
    }

    [HttpPost("tokens/{token}/revoke")]
    public async Task<IActionResult> Revoke(string token, RevokeQueueRequest request)
    {
        var session = await db.QueueSessions.SingleOrDefaultAsync(x => x.QueueToken == token);
        if (session == null) return NotFound(new { code = "TOKEN_INVALID" });
        var config = await db.QueueConfigurations.SingleOrDefaultAsync(x => x.EventId == session.EventId);
        if (config == null) return NotFound(new { code = "EVENT_NOT_FOUND" });
        await redis.RevokeAsync(session.EventId, session.UserId, token, config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
        session.Status = "REVOKED";
        session.UpdatedAt = QueueTimeZone.Now;
        await db.SaveChangesAsync();
        return Ok(new { message = "Queue token revoked successfully." });
    }

    [HttpPost("sessions/{sessionId}/revoke")]
    public async Task<IActionResult> RevokeSession(string sessionId, RevokeQueueRequest request)
    {
        var session = await db.QueueSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId);
        if (session == null) return NotFound(new { code = "TOKEN_INVALID" });
        var config = await db.QueueConfigurations.SingleOrDefaultAsync(x => x.EventId == session.EventId);
        if (config == null) return NotFound(new { code = "EVENT_NOT_FOUND" });
        await redis.RevokeAsync(session.EventId, session.UserId, session.QueueToken, config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
        session.Status = "REVOKED";
        session.UpdatedAt = QueueTimeZone.Now;
        await db.SaveChangesAsync();
        return Ok(new { message = "Queue token revoked successfully." });
    }

    [HttpPost("sessions/{sessionId}/unrevoke")]
    public async Task<IActionResult> UnrevokeSession(string sessionId)
    {
        var session = await db.QueueSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId);
        if (session == null) return NotFound(new { code = "TOKEN_INVALID" });
        if (session.Status != "REVOKED") return Conflict(new { code = "SESSION_NOT_REVOKED" });

        // The revoked token remains invalid. The customer can register again
        // and receive a new token because RegisterAsync accepts LEFT sessions.
        session.Status = "LEFT";
        session.Position = null;
        session.UpdatedAt = QueueTimeZone.Now;
        await db.SaveChangesAsync();
        return Ok(new { message = "Queue session restored. The user can register again." });
    }

    [HttpGet("abuse-alerts")]
    public async Task<ActionResult<IEnumerable<AbuseAlertResponse>>> Alerts([FromQuery] string? severity = null, [FromQuery] string? status = null)
    {
        var query = db.AbuseAlerts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(severity)) query = query.Where(x => x.Severity == severity.ToUpper());
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.ToUpper());
        var alerts = await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync();
        return Ok(alerts.Select(ToResponse));
    }

    [HttpGet("abuse-alerts/{id:long}")]
    public async Task<ActionResult<AbuseAlertResponse>> Alert(long id)
    {
        var alert = await db.AbuseAlerts.AsNoTracking().SingleOrDefaultAsync(x => x.AbuseAlertId == id);
        return alert == null ? NotFound() : Ok(ToResponse(alert));
    }

    [HttpPost("sessions/{sessionId}/flag")]
    public Task<IActionResult> Flag(string sessionId, SessionActionRequest request) => RecordAlert(sessionId, "FLAGGED", "MEDIUM", request.Reason);

    [HttpPost("sessions/{sessionId}/block")]
    public Task<IActionResult> Block(string sessionId, SessionActionRequest request) => RecordAlert(sessionId, "BLOCKED", "HIGH", request.Reason);

    [HttpPost("sessions/{sessionId}/unblock")]
    public async Task<IActionResult> Unblock(string sessionId)
    {
        var session = await db.QueueSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId);
        if (session == null) return NotFound();
        session.Status = "LEFT";
        session.UpdatedAt = QueueTimeZone.Now;
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<IActionResult> RecordAlert(string sessionId, string status, string severity, string? reason)
    {
        var session = await db.QueueSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId);
        if (session == null) return NotFound();
        if (status == "BLOCKED")
        {
            var config = await db.QueueConfigurations.SingleOrDefaultAsync(x => x.EventId == session.EventId);
            if (config == null) return NotFound(new { code = "EVENT_NOT_FOUND" });
            await redis.RevokeAsync(session.EventId, session.UserId, session.QueueToken, config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
        }
        session.Status = status;
        session.UpdatedAt = QueueTimeZone.Now;
        db.AbuseAlerts.Add(new AbuseAlert { EventId = session.EventId, UserId = session.UserId, SessionId = session.SessionId, QueueToken = session.QueueToken, AlertType = status == "BLOCKED" ? "SESSION_BLOCKED" : "MANUAL_FLAG", Severity = severity, Status = "OPEN", DetectedBehavior = reason ?? "Manual review" });
        await db.SaveChangesAsync();
        return Ok(new { status });
    }

    private static AbuseAlertResponse ToResponse(AbuseAlert x) => new(x.AbuseAlertId, x.EventId, x.UserId, x.SessionId, x.QueueToken[..8] + "...", x.AlertType, x.Severity, x.Status, x.DetectedBehavior, QueueTimeZone.AsVietnamOffset(x.CreatedAt));
}
