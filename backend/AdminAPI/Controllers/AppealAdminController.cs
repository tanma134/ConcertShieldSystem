using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    /// <summary>UC_18.7 Review & Resolve Appeal (Admin, Staff).</summary>
    [ApiController]
    [Route("api/admin/risk/appeals")]
    [Authorize(Policy = "CanReviewFraud")]
    public class AppealAdminController : ControllerBase
    {
        private readonly IAppealService _service;
        private readonly IAuditLogService _audit;
        private readonly ILogger<AppealAdminController> _logger;

        public AppealAdminController(IAppealService service, IAuditLogService audit, ILogger<AppealAdminController> logger)
        {
            _service = service;
            _audit = audit;
            _logger = logger;
        }

        // GET api/admin/risk/appeals?status=Pending
        [HttpGet]
        public Task<IActionResult> Search([FromQuery] AppealQuery query) =>
            Run(async () => Ok(await _service.SearchAppealsAsync(query)));

        // GET api/admin/risk/appeals/5
        [HttpGet("{id:int}")]
        public Task<IActionResult> Detail(int id) =>
            Run(async () => Ok(await _service.GetAppealDetailAsync(id)));

        // POST api/admin/risk/appeals/5/resolve
        [HttpPost("{id:int}/resolve")]
        public Task<IActionResult> Resolve(int id, [FromBody] ResolveAppealRequest req) =>
            Run(async () =>
            {
                var staffId = GetCurrentUserId();
                var result = await _service.ResolveAppealAsync(id, req, staffId);
                await _audit.LogAsync(
                    userId: staffId,
                    actorType: User.IsInRole("Admin") ? "admin" : "staff",
                    action: "APPEAL_RESOLVED",
                    entityType: "RiskAppeal",
                    entityId: result.RiskAppealId.ToString(),
                    newValue: new { req.Decision, req.ReviewNote, result.Status },
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    userAgent: Request.Headers.UserAgent.ToString());
                return Ok(result);
            });

        private int GetCurrentUserId()
        {
            var raw = (User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            return int.TryParse(raw, out var id) ? id : throw new AppealServiceException(401, "Token không chứa userId hợp lệ.");
        }

        private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
        {
            try { return await action(); }
            catch (AppealServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in appeal admin endpoint");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }
    }
}