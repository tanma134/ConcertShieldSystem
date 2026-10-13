using System.Security.Claims;
using AdminAPI.DTOs;
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

        public RiskDecisionController(IAppealService service, IAuthorizationService authz,
            ILogger<RiskDecisionController> logger)
        {
            _service = service;
            _authz = authz;
            _logger = logger;
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
                var canViewFull = (await _authz.AuthorizeAsync(User, "CanReviewFraud")).Succeeded;
                if (canViewFull)
                    return Ok(await _service.GetAdminDecisionAsync(id, ct));

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