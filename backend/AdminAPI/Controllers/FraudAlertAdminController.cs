using AdminAPI.DTOs;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    /// <summary>UC_18.1 Fraud Alert Dashboard, UC_18.2 Fraud Alert Details (Admin, Staff, chỉ đọc).</summary>
    [ApiController]
    [Route("api/admin/fraud-alerts")]
    [Authorize(Policy = "CanReviewFraud")]
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
        public Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
            Run(async () => Ok(await _service.GetSummaryAsync(from, to)));

        [HttpGet]
        public Task<IActionResult> Search([FromQuery] FraudAlertQuery query) =>
            Run(async () => Ok(await _service.SearchAsync(query)));

        [HttpGet("{id:int}")]
        public Task<IActionResult> Detail(int id) =>
            Run(async () => Ok(await _service.GetDetailAsync(id)));

        [HttpPost("{id:int}/ai-summary")]
        public Task<IActionResult> AiSummary(int id, CancellationToken ct) =>
            Run(async () => Ok(await _ai.SummarizeAlertAsync(id, ct)));

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