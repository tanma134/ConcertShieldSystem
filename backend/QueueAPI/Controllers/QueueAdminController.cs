using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueAPI.DTOs;
using QueueAPI.Models;
using QueueAPI.Repositories;
using QueueAPI.Services;
using QueueAPI.Time;

namespace QueueAPI.Controllers;

[ApiController]
[Route("api/queue/admin")]
[Authorize(Policy = "QueueAdmin")]
public sealed class QueueAdminController(IQueueRepository repository, IRedisQueueStore redis, IQueueAuditLogClient auditLog) : ControllerBase
{
    private static readonly string[] SupportedAlertTypes =
    [
        "MULTIPLE_REGISTRATION_ATTEMPTS",
        "AUTOMATED_REQUEST_PATTERN",
        "TOKEN_REPLAY"
    ];

    private static readonly string[] SupportedSeverities = ["LOW", "MEDIUM", "HIGH"];

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "0");

    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<QueueAdminSessionResponse>>> Sessions(
        [FromQuery] int? eventId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            return BadRequest(new { code = "INVALID_DATE_RANGE" });

        var updatedAtFrom = fromDate?.ToDateTime(TimeOnly.MinValue);
        DateTime? updatedAtBefore = toDate.HasValue && toDate.Value < DateOnly.MaxValue
            ? toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : null;
        var sessions = await repository.GetAdminSessionsAsync(eventId, updatedAtFrom, updatedAtBefore);
        return Ok(sessions.Select(x => new QueueAdminSessionResponse(x.QueueSessionId, x.EventId, x.UserId, x.SessionId, x.Status, x.Position, x.QueueToken[..8] + "...", QueueTimeZone.AsVietnamOffset(x.UpdatedAt))));
    }

    [HttpPost("tokens/{token}/revoke")]
    public async Task<IActionResult> Revoke(string token, RevokeQueueRequest request)
    {
        var session = await repository.GetSessionByTokenAsync(token);
        if (session == null) return NotFound(new { code = "TOKEN_INVALID" });
        var config = await repository.GetConfigurationByEventIdAsync(session.EventId);
        if (config == null) return NotFound(new { code = "EVENT_NOT_FOUND" });
        await redis.RevokeAsync(session.EventId, session.UserId, session.SessionId, token, config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
        session.Status = "REVOKED";
        session.UpdatedAt = QueueTimeZone.Now;
        await repository.SaveChangesAsync();
        return Ok(new { message = "Queue token revoked successfully." });
    }

    [HttpPost("sessions/{sessionId}/revoke")]
    public async Task<IActionResult> RevokeSession(string sessionId, RevokeQueueRequest request)
    {
        var session = await repository.GetSessionByIdAsync(sessionId);
        if (session == null) return NotFound(new { code = "TOKEN_INVALID" });
        var config = await repository.GetConfigurationByEventIdAsync(session.EventId);
        if (config == null) return NotFound(new { code = "EVENT_NOT_FOUND" });
        await redis.RevokeAsync(session.EventId, session.UserId, session.SessionId, session.QueueToken, config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
        session.Status = "REVOKED";
        session.UpdatedAt = QueueTimeZone.Now;
        await repository.SaveChangesAsync();
        return Ok(new { message = "Queue token revoked successfully." });
    }

    [HttpPost("sessions/{sessionId}/unrevoke")]
    public async Task<IActionResult> UnrevokeSession(string sessionId)
    {
        var session = await repository.GetSessionByIdAsync(sessionId);
        if (session == null) return NotFound(new { code = "TOKEN_INVALID" });
        if (session.Status != "REVOKED") return Conflict(new { code = "SESSION_NOT_REVOKED" });

        // The revoked token remains invalid. The customer can register again
        // and receive a new token because RegisterAsync accepts LEFT sessions.
        session.Status = "LEFT";
        session.Position = null;
        session.UpdatedAt = QueueTimeZone.Now;
        await repository.SaveChangesAsync();
        return Ok(new { message = "Queue session restored. The user can register again." });
    }

    [HttpGet("abuse-alerts")]
    public async Task<ActionResult<IEnumerable<AbuseAlertResponse>>> Alerts([FromQuery] string? severity = null, [FromQuery] string? status = null)
    {
        var alerts = await repository.GetAdminAlertsAsync(
            SupportedAlertTypes,
            SupportedSeverities,
            string.IsNullOrWhiteSpace(severity) ? null : severity.ToUpperInvariant(),
            string.IsNullOrWhiteSpace(status) ? null : status.ToUpperInvariant());
        return Ok(alerts.Select(ToResponse));
    }

    [HttpGet("abuse-alerts/{id:long}")]
    public async Task<ActionResult<AbuseAlertResponse>> Alert(long id)
    {
        var alert = await repository.GetAdminAlertByIdAsync(id, SupportedAlertTypes, SupportedSeverities);
        return alert == null ? NotFound() : Ok(ToResponse(alert));
    }

    [HttpPost("sessions/{sessionId}/flag")]
    public Task<IActionResult> Flag(string sessionId, SessionActionRequest request) =>
        RecordSessionAction(sessionId, "FLAGGED", request.AlertId);

    [HttpPost("sessions/{sessionId}/block")]
    public Task<IActionResult> Block(string sessionId, SessionActionRequest request) =>
        RecordSessionAction(sessionId, "BLOCKED", request.AlertId);

    [HttpPost("sessions/{sessionId}/unblock")]
    public async Task<IActionResult> Unblock(string sessionId, [FromQuery] long? alertId = null)
    {
        var session = await repository.GetSessionByIdAsync(sessionId);
        if (session == null) return NotFound();
        var alert = await FindRelatedAlertAsync(sessionId, alertId);
        if (alertId.HasValue && alert == null) return NotFound();
        var oldSessionStatus = session.Status;
        var oldAlertStatus = alert?.Status;
        session.Status = "LEFT";
        session.UpdatedAt = QueueTimeZone.Now;
        if (alert != null) alert.Status = "OPEN";
        await repository.SaveChangesAsync();
        await WriteAuditLogAsync(
            "QUEUE_ABUSE_ALERT_UNBLOCKED",
            alert,
            session,
            new { alertStatus = oldAlertStatus, sessionStatus = oldSessionStatus },
            new { alertStatus = alert?.Status, sessionStatus = session.Status });
        return Ok(new { status = "OPEN", sessionStatus = session.Status });
    }

    private async Task<IActionResult> RecordSessionAction(string sessionId, string status, long? alertId)
    {
        var session = await repository.GetSessionByIdAsync(sessionId);
        if (session == null) return NotFound();
        var alert = await FindRelatedAlertAsync(sessionId, alertId);
        if (alertId.HasValue && alert == null) return NotFound();
        var oldSessionStatus = session.Status;
        var oldAlertStatus = alert?.Status;
        if (status == "BLOCKED")
        {
            var config = await repository.GetConfigurationByEventIdAsync(session.EventId);
            if (config == null) return NotFound(new { code = "EVENT_NOT_FOUND" });
            await redis.RevokeAsync(session.EventId, session.UserId, session.SessionId, session.QueueToken, config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
            session.Status = "BLOCKED";
        }
        session.UpdatedAt = QueueTimeZone.Now;
        if (alert != null) alert.Status = status;
        await repository.SaveChangesAsync();
        await WriteAuditLogAsync(
            status == "BLOCKED" ? "QUEUE_ABUSE_ALERT_BLOCKED" : "QUEUE_ABUSE_ALERT_FLAGGED",
            alert,
            session,
            new { alertStatus = oldAlertStatus, sessionStatus = oldSessionStatus },
            new { alertStatus = alert?.Status, sessionStatus = session.Status });
        return Ok(new { status, sessionStatus = session.Status });
    }

    private Task WriteAuditLogAsync(
        string action,
        AbuseAlert? alert,
        QueueSession session,
        object oldValue,
        object newValue) =>
        auditLog.LogAsync(
            UserId,
            action,
            alert == null ? "QueueSession" : "QueueAbuseAlert",
            alert?.AbuseAlertId.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? session.SessionId,
            oldValue,
            newValue,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.TraceIdentifier,
            Request.Headers.UserAgent.ToString());

    private Task<AbuseAlert?> FindRelatedAlertAsync(string sessionId, long? alertId) =>
        repository.GetRelatedAlertAsync(sessionId, alertId, SupportedAlertTypes, SupportedSeverities);

    private static AbuseAlertResponse ToResponse(AbuseAlert alert)
    {
        var tokenFingerprint = alert.TokenFingerprint.StartsWith("sha256:", StringComparison.Ordinal) ||
            alert.TokenFingerprint.StartsWith("md5:", StringComparison.Ordinal)
            ? alert.TokenFingerprint
            : QueueAbuseDetection.TokenFingerprint(alert.TokenFingerprint);
        return new(
            alert.AbuseAlertId,
            alert.EventId,
            alert.UserId,
            alert.SessionId,
            tokenFingerprint,
            alert.AlertType,
            alert.Severity,
            alert.Status,
            alert.DetectedBehavior,
            QueueTimeZone.AsVietnamOffset(alert.CreatedAt));
    }
}
