using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Repositories;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{

    [ApiController]
    [Route("api/risk/decisions")]
    [Authorize]
    public class RiskDecisionController : ControllerBase
    {
        private readonly IAppealService _service;
        private readonly ILogger<RiskDecisionController> _logger;
        private readonly IAuthorizationService _authz;
        private readonly IFraudAlertRepository _alerts;

        public RiskDecisionController(IAppealService service, IAuthorizationService authz,
            ILogger<RiskDecisionController> logger, IFraudAlertRepository alerts)
        {
            _service = service;
            _authz = authz;
            _logger = logger;
            _alerts = alerts;
        }

        // GET api/risk/decisions/me - decision của chính user đang đăng nhập (My Profile -> Account Restrictions)
        [HttpGet("me")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            try
            {
                return Ok(await _service.GetMyDecisionsAsync(GetCurrentUserId(), ct));
            }
            catch (AppealServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error listing my risk decisions");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id, CancellationToken ct)
        {
            try
            {
                // Staff reviewing a fraud case need the full decision (not the customer view).
                var canViewFull = (await _authz.AuthorizeAsync(User, "CanReviewFraudCases")).Succeeded;
                if (canViewFull)
                {
                    var dto = await _service.GetAdminDecisionAsync(id, ct);

                    // BR-236: Staff only see the full decision of a case assigned to them.
                    if (User.IsInRole("Staff") && !User.IsInRole("Admin"))
                    {
                        var me = GetCurrentUserId();
                        var assigned = dto.FraudAlertId is int alertId
                            && await _alerts.IsAssignedToAsync(alertId, me, ct);
                        if (!assigned)
                            return StatusCode(403, new { message = "This decision belongs to a case not assigned to you." });
                    }

                    return Ok(dto);
                }

                var userId = GetCurrentUserId();
                return Ok(await _service.GetPublicDecisionAsync(id, userId, ct));
            }
            catch (AppealServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error reading risk decision {Id}", id);
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }

        private int GetCurrentUserId()
        {
            var raw = (User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            return int.TryParse(raw, out var id) ? id : throw new AppealServiceException(401, "Token does not contain a valid userId");
        }
    }
}
