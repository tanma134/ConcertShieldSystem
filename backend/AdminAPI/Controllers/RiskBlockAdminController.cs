using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/risk/blocks")]
    [Authorize(Policy = "CanReviewFraud")]
    public class RiskBlockAdminController : ControllerBase
    {
        private readonly IFraudAlertService _service;
        private readonly IAuditLogService _audit;
        private readonly ILogger<RiskBlockAdminController> _logger;

        public RiskBlockAdminController(IFraudAlertService service, IAuditLogService audit,
            ILogger<RiskBlockAdminController> logger)
        {
            _service = service;
            _audit = audit;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> Search([FromQuery] RiskBlockQuery query) =>
            Run(async () => Ok(await _service.SearchBlocksAsync(query)));


        [HttpPost]
        public Task<IActionResult> Block([FromBody] BlockRequest req) =>
            Run(async () =>
            {
                var staffId = GetCurrentUserId();
                var result = await _service.BlockAsync(req, staffId);
                await WriteAuditAsync("RISK_BLOCK_CREATED", result, staffId, new { req.Scope, req.DurationHours, req.Reason });
                return Ok(result);
            });

        [HttpPost("{id:int}/restore")]
        public Task<IActionResult> Restore(int id, [FromBody] RestoreRequest req) =>
            Run(async () =>
            {
                var staffId = GetCurrentUserId();
                var result = await _service.RestoreAsync(id, req.Reason, staffId);
                await WriteAuditAsync("RISK_BLOCK_RESTORED", result, staffId, new { req.Reason });
                return Ok(result);
            });

        private Task WriteAuditAsync(string action, RiskBlockResultDto result, int staffId, object detail) =>
            _audit.LogAsync(
                userId: staffId,
                actorType: User.IsInRole("Admin") ? "admin" : "staff",
                action: action,
                entityType: "RiskDecision",
                entityId: result.RiskDecisionId.ToString(),
                newValue: new { result.DecisionCode, result.Scope, result.Status, result.UserId, detail },
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

        private int GetCurrentUserId()
        {
            var raw = (User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            return int.TryParse(raw, out var id) ? id : throw new RiskServiceException(401, "Token does not contain a valid userId.");
        }

        private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
        {
            try { return await action(); }
            catch (RiskServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in risk block endpoint");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }
    }
}