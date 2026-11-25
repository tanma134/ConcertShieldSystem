using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/fraud-alerts")]
    // Admin and Staff may view alert details (read-only). All block/restore actions live in RiskBlockAdminController (Admin only).
    [Authorize(Policy = "CanReviewFraudCases")]
    public class FraudAlertAdminController : ControllerBase
    {
        private readonly IFraudAlertService _service;
        private readonly IAiAnalysisService _ai;
        private readonly ILogger<FraudAlertAdminController> _logger;

        public FraudAlertAdminController(IFraudAlertService service, IAiAnalysisService ai,
            ILogger<FraudAlertAdminController> logger)
        {
            _service = service;
            _ai = ai;
            _logger = logger;
        }


        [HttpGet("summary")]
        [Authorize(Roles = "Admin")]
        public Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
            Run(async () => Ok(await _service.GetSummaryAsync(from, to)));

  
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public Task<IActionResult> Search([FromQuery] FraudAlertQuery query) =>
            Run(async () => Ok(await _service.SearchAsync(query)));


        [HttpGet("{id:int}")]
        public Task<IActionResult> Detail(int id) =>
            Run(async () =>
            {
                var detail = await _service.GetDetailAsync(id);

                if (IsStaffOnly)
                {
                    var me = CurrentUserId;
                    if (me == null || detail.Alert.AssignedTo != me)
                        return StatusCode(403, new { message = "This case is not assigned to you." });
                }

                return Ok(detail);
            });

        [HttpPost("{id:int}/ai-summary")]
        [Authorize(Roles = "Admin")]
        public Task<IActionResult> AiSummary(int id, CancellationToken ct) =>
            Run(async () => Ok(await _ai.SummarizeAlertAsync(id, ct)));

        private bool IsStaffOnly => User.IsInRole("Staff") && !User.IsInRole("Admin");

        private int? CurrentUserId =>
            int.TryParse((User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
                ? id : null;

        private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
        {
            try { return await action(); }
            catch (RiskServiceException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in fraud alert endpoint");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }
    }
}
