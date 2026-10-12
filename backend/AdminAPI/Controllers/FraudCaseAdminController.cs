using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    /// <summary>UC_18.8 View Fraud Case List, UC_18.9 Assign Fraud Case to Analyst, UC_18.10 Resolve Fraud Case.</summary>
    [ApiController]
    [Route("api/admin/fraud-cases")]
    [Authorize(Policy = "CanReviewFraud")]
    public class FraudCaseAdminController : ControllerBase
    {
        private readonly IFraudCaseService _service;
        private readonly IAuditLogService _audit;
        private readonly ILogger<FraudCaseAdminController> _logger;

        public FraudCaseAdminController(IFraudCaseService service, IAuditLogService audit,
            ILogger<FraudCaseAdminController> logger)
        {
            _service = service; _audit = audit; _logger = logger;
        }

        // GET api/admin/fraud-cases?status=Open&unassigned=true&page=1
        [HttpGet]
        public Task<IActionResult> List([FromQuery] FraudCaseQuery query) =>
            Run(async () => Ok(await _service.SearchAsync(query)));

        // POST api/admin/fraud-cases/5/assign
        [HttpPost("{id:int}/assign")]
        public Task<IActionResult> Assign(int id, [FromBody] AssignFraudCaseRequest req) =>
            Run(async () =>
            {
                var actor = GetCurrentUserId();
                var r = await _service.AssignAsync(id, req.AnalystId, actor);
                await WriteAuditAsync("FRAUD_CASE_ASSIGNED", r, actor,
                    new { r.PreviousStatus, r.PreviousAssignee, r.AssignedTo, req.Note });
                return Ok(r);
            });

        // POST api/admin/fraud-cases/5/resolve
        [HttpPost("{id:int}/resolve")]
        public Task<IActionResult> Resolve(int id, [FromBody] ResolveFraudCaseRequest req) =>
            Run(async () =>
            {
                var actor = GetCurrentUserId();
                var r = await _service.ResolveAsync(id, req.Outcome, req.Note, actor);
                await WriteAuditAsync("FRAUD_CASE_RESOLVED", r, actor,
                    new { r.PreviousStatus, r.PreviousAssignee, req.Outcome, req.Note });
                return Ok(r);
            });

        private Task WriteAuditAsync(string action, FraudCaseResultDto r, int actor, object detail) =>
            _audit.LogAsync(
                userId: actor,
                actorType: User.IsInRole("Admin") ? "admin" : "staff",
                action: action,
                entityType: "FraudAlert",
                entityId: r.CaseId.ToString(),
                newValue: new { r.CaseId, r.Status, detail },
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
                _logger.LogError(ex, "Unexpected error in fraud case endpoint");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }
    }
}
