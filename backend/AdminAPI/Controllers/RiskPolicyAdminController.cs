using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    /// <summary>UC_18.11 Configure Risk Policy Parameters (Admin). Risk Engine đọc lại cấu hình sau tối đa 60 giây.</summary>
    [ApiController]
    [Route("api/admin/risk/policy")]
    [Authorize(Policy = "CanReviewFraud")]
    public class RiskPolicyAdminController : ControllerBase
    {
        private readonly IRiskPolicyService _service;
        private readonly IAuditLogService _audit;
        private readonly ILogger<RiskPolicyAdminController> _logger;

        public RiskPolicyAdminController(IRiskPolicyService service, IAuditLogService audit,
            ILogger<RiskPolicyAdminController> logger)
        {
            _service = service; _audit = audit; _logger = logger;
        }

        // GET api/admin/risk/policy?category=risk_config
        [HttpGet]
        public Task<IActionResult> Get([FromQuery] string? category) =>
            Run(async () => Ok(await _service.GetAsync(category)));

        // PUT api/admin/risk/policy
        [HttpPut]
        public Task<IActionResult> Update([FromBody] UpdateRiskPolicyRequest req) =>
            Run(async () =>
            {
                var admin = GetCurrentUserId();
                var result = await _service.UpdateAsync(req, admin);
                await _audit.LogAsync(
                    userId: admin,
                    actorType: "admin",
                    action: "RISK_POLICY_UPDATED",
                    entityType: "SystemParameter",
                    entityId: string.Join(",", result.Changes.Select(c => c.Key)),
                    oldValue: result.Changes.ToDictionary(c => c.Key, c => c.OldValue),
                    newValue: new { Changes = result.Changes.ToDictionary(c => c.Key, c => c.NewValue), req.Reason },
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    userAgent: Request.Headers.UserAgent.ToString());
                return Ok(result);
            });

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
                _logger.LogError(ex, "Unexpected error in risk policy endpoint");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }
    }
}
