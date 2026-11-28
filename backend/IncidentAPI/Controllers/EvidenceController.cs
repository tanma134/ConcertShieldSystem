using System.Security.Claims;
using IncidentAPI.DTOs;
using IncidentAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentAPI.Controllers;

[ApiController]
[Route("api/incidents/{incidentId:int}/evidences")]
[Route("api/incidents/{incidentId:int}/evidence")]
[Authorize]
public class EvidenceController : ControllerBase
{
    private readonly IEvidenceService _evidenceService;
    private readonly ILogger<EvidenceController> _logger;
    private readonly IAuditLogClient _auditLog;

    public EvidenceController(IEvidenceService evidenceService, ILogger<EvidenceController> logger, IAuditLogClient auditLog)
    {
        _evidenceService = evidenceService;
        _logger = logger;
        _auditLog = auditLog;
    }

    private Task AuditAsync(int userId, string action, int incidentId, string? entityId, object? newValue = null) =>
        _auditLog.LogAsync(userId, User.IsInRole("Admin") ? "admin" : "staff", action, "IncidentEvidence", entityId,
            newValue: newValue is null ? new { incidentId } : newValue,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString());

    [HttpGet]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> GetAll(int incidentId) =>
        Run(async () =>
        {
            var evidences = await _evidenceService.GetEvidencesAsync(incidentId);
            return Ok(evidences);
        });

    [HttpPost]
    [Authorize(Policy = "CanManageIncidents")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public Task<IActionResult> Upload(
        int incidentId,
        [FromForm] IFormFile file,
        [FromForm] string? description = null,
        [FromForm] string? caption = null) =>
        Run(async () =>
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select a file to upload." });

            var finalCaption = !string.IsNullOrWhiteSpace(caption) ? caption : description;
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();
            var isAdmin = User.IsInRole("Admin");
            var result = await _evidenceService.UploadEvidenceAsync(
                incidentId, file, finalCaption, userId, userName, isAdmin);
            await AuditAsync(userId, "INCIDENT_EVIDENCE_ADDED", incidentId, incidentId.ToString(),
                new { incidentId, fileName = file.FileName, size = file.Length, contentType = file.ContentType });
            return Ok(result);
        });

    [HttpDelete("{evidenceId:int}")]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> Delete(int incidentId, int evidenceId) =>
        Run(async () =>
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            await _evidenceService.DeleteEvidenceAsync(incidentId, evidenceId, userId, isAdmin);
            await AuditAsync(userId, "INCIDENT_EVIDENCE_DELETED", incidentId, evidenceId.ToString());
            return Ok(new { message = "Evidence deleted successfully." });
        });

    private int GetCurrentUserId()
    {
        var raw = (User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
        return int.TryParse(raw, out var id)
            ? id
            : throw new IncidentServiceException(401, "Token does not contain a valid userId.");
    }

    private string GetCurrentUserName()
    {
        return User.FindFirst("fullName")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? "Staff";
    }

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (IncidentServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in evidence endpoint");
            return StatusCode(500, new { message = "An unexpected error occurred while processing the evidence." });
        }
    }
}
