using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    [ApiController]
    [Route("api/risk/appeals")]
    [Authorize]
    public class AppealController : ControllerBase
    {
        private readonly IAppealService _service;
        private readonly ILogger<AppealController> _logger;

        public AppealController(IAppealService service, ILogger<AppealController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] SubmitAppealRequest req, CancellationToken ct)
        {
            try
            {
                var userId = GetCurrentUserId();
                return Ok(await _service.SubmitAppealAsync(req, userId, ct));
            }
            catch (AppealServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error submitting appeal");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }

        private int GetCurrentUserId()
        {
            var raw = (User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            return int.TryParse(raw, out var id) ? id : throw new AppealServiceException(401, "Token không chứa userId hợp lệ.");
        }
    }
}