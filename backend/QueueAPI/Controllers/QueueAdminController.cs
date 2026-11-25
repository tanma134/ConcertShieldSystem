using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueAPI.DTOs;
using QueueAPI.Services;

namespace QueueAPI.Controllers;

[ApiController]
[Route("api/queue/admin")]
[Authorize(Policy = "QueueAdmin")]
public sealed class QueueAdminController(IQueueAdminService adminService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "0");

    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<QueueAdminSessionResponse>>> Sessions(
        [FromQuery] int? eventId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            return BadRequest(new { code = "INVALID_DATE_RANGE" });

        return Ok(await adminService.GetSessionsAsync(eventId, fromDate, toDate));
    }

    [HttpPost("tokens/{token}/revoke")]
    public async Task<IActionResult> Revoke(string token, RevokeQueueRequest request)
    {
        var result = await adminService.RevokeTokenAsync(token);
        if (result.Status != QueueAdminResultStatus.Success)
            return MapFailure(result);

        return Ok(new { message = result.Value });
    }

    [HttpPost("sessions/{sessionId}/revoke")]
    public async Task<IActionResult> RevokeSession(string sessionId, RevokeQueueRequest request)
    {
        var result = await adminService.RevokeSessionAsync(sessionId);
        if (result.Status != QueueAdminResultStatus.Success)
            return MapFailure(result);

        return Ok(new { message = result.Value });
    }

    [HttpPost("sessions/{sessionId}/unrevoke")]
    public async Task<IActionResult> UnrevokeSession(string sessionId)
    {
        var result = await adminService.UnrevokeSessionAsync(sessionId);
        if (result.Status != QueueAdminResultStatus.Success)
            return MapFailure(result);

        return Ok(new { message = result.Value });
    }

    [HttpGet("abuse-alerts")]
    public async Task<ActionResult<IEnumerable<AbuseAlertResponse>>> Alerts(
        [FromQuery] string? severity = null,
        [FromQuery] string? status = null) =>
        Ok(await adminService.GetAlertsAsync(severity, status));

    [HttpGet("abuse-alerts/{id:long}")]
    public async Task<ActionResult<AbuseAlertResponse>> Alert(long id)
    {
        var alert = await adminService.GetAlertAsync(id);
        return alert == null ? NotFound() : Ok(alert);
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
        var result = await adminService.UnblockSessionAsync(sessionId, alertId, CreateAuditContext());
        if (result.Status != QueueAdminResultStatus.Success)
            return MapFailure(result);

        return Ok(new
        {
            status = result.Value!.Status,
            sessionStatus = result.Value.SessionStatus
        });
    }

    private async Task<IActionResult> RecordSessionAction(string sessionId, string status, long? alertId)
    {
        var result = await adminService.RecordSessionActionAsync(
            sessionId,
            status,
            alertId,
            CreateAuditContext());
        if (result.Status != QueueAdminResultStatus.Success)
            return MapFailure(result);

        return Ok(new
        {
            status = result.Value!.Status,
            sessionStatus = result.Value.SessionStatus
        });
    }

    private QueueAdminAuditContext CreateAuditContext() =>
        new(
            UserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.TraceIdentifier,
            Request.Headers.UserAgent.ToString());

    private static IActionResult MapFailure<T>(QueueAdminResult<T> result) =>
        result.Status switch
        {
            QueueAdminResultStatus.NotFound when result.ErrorCode != null =>
                new NotFoundObjectResult(new { code = result.ErrorCode }),
            QueueAdminResultStatus.NotFound => new NotFoundResult(),
            QueueAdminResultStatus.Conflict => new ConflictObjectResult(new { code = result.ErrorCode }),
            _ => throw new InvalidOperationException($"Unexpected queue admin result status: {result.Status}.")
        };
}
