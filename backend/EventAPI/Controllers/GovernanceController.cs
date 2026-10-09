using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EventAPI.Controllers;
[Route("api/events/{eventId:int}/governance")]
[Authorize(Roles = "Organizer,Admin")]
public class GovernanceController(GovernanceService service) : BaseApiController
{
    // Response wrapper giống EventAPI hiện có; middleware xử lý lỗi thống nhất.
    private IActionResult Data(object value) => Ok(new { success = true, data = value });
    [HttpGet("compliance")]
    public async Task<IActionResult> Compliance(int eventId, CancellationToken ct) => Data(await service.ComplianceAsync(eventId, CurrentUserId, IsAdmin, ct));
    [HttpPost("compliance/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(22 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int eventId, [FromForm] string documentType, IFormFile file, CancellationToken ct)
    { if (!IsOrganizer) return Forbid(); await service.UploadAsync(eventId, CurrentUserId, documentType, file, ct); return Data(await service.ComplianceAsync(eventId, CurrentUserId, false, ct)); }
    [HttpGet("compliance/documents/{documentId:long}/download")]
    public async Task<IActionResult> Download(int eventId, long documentId, CancellationToken ct)
    {
        var file = await service.DownloadAsync(eventId, documentId, CurrentUserId, IsAdmin, ct);
        Response.Headers.CacheControl = "no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Bytes, file.Mime, file.Name);
    }
    [HttpPost("compliance/reviews")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewCompliance(int eventId, ComplianceReviewInput input, CancellationToken ct)
    { await service.ReviewComplianceAsync(eventId, CurrentUserId, input, ct); return Data(await service.ComplianceAsync(eventId, CurrentUserId, true, ct)); }
    [HttpPost("changes/preview")]
    public async Task<IActionResult> Preview(int eventId, ChangeInput input, CancellationToken ct) => Data(await service.PreviewAsync(eventId, CurrentUserId, IsAdmin, input, ct));
    [HttpPost("changes")]
    public async Task<IActionResult> Submit(int eventId, ChangeInput input, CancellationToken ct)
    { if (!IsOrganizer) return Forbid(); return Data(new { id = await service.SubmitChangeAsync(eventId, CurrentUserId, input, ct) }); }
    [HttpGet("changes")]
    public async Task<IActionResult> Changes(int eventId, CancellationToken ct) => Data(await service.ChangesAsync(eventId, CurrentUserId, IsAdmin, ct));
    [HttpPost("changes/{requestId:long}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewChange(int eventId, long requestId, ChangeReviewInput input, CancellationToken ct)
    { await service.ReviewChangeAsync(eventId, requestId, CurrentUserId, input, ct); return Data(await service.ChangesAsync(eventId, CurrentUserId, true, ct)); }
    [HttpGet("changes/{requestId:long}/affected")]
    public async Task<IActionResult> Affected(int eventId, long requestId, string tab = "orders", string? status = null, string? orderCode = null, int? userId = null, int page = 1, int pageSize = 10, CancellationToken ct = default)
        => Data(await service.AffectedAsync(eventId, requestId, CurrentUserId, IsAdmin, tab, status, orderCode, userId, page, pageSize, ct));
}
[Route("api/admin/change-requests")]
[Authorize(Roles = "Admin")]
public class AdminChangeRequestsController(GovernanceService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List(string? status = null, CancellationToken ct = default) => Ok(new { success = true, data = await service.AllChangesAsync(status, ct) });
}
[ApiController]
[Route("api/internal/events/{eventId:int}/eligibility")]
public class SaleEligibilityController(GovernanceService service, IConfiguration config) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int eventId, CancellationToken ct)
    {
        var key = config["Governance:InternalApiKey"];
        if (string.IsNullOrEmpty(key) || Request.Headers["X-Internal-Api-Key"] != key) return Unauthorized();
        return Ok(await service.EligibilityAsync(eventId, ct));
    }
}
