using AdminAPI.DTOs;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    /// <summary>
    /// Endpoint NỘI BỘ: TicketAPI gửi các rule đã khớp sang đây. AdminAPI tự tính điểm, ra quyết định và lưu.
    /// Bảo vệ bằng header X-Internal-Api-Key (giống InternalAuditLogController).
    /// KHÔNG public route này ra Ocelot Gateway cho client/browser.
    /// </summary>
    [ApiController]
    [Route("api/internal/fraud-alerts")]
    public class InternalFraudAlertController : ControllerBase
    {
        private readonly IFraudAlertService _service;
        private readonly IAuditLogService _audit;
        private readonly IConfiguration _config;
        private readonly ILogger<InternalFraudAlertController> _logger;

        public InternalFraudAlertController(IFraudAlertService service, IAuditLogService audit, IConfiguration config,
            ILogger<InternalFraudAlertController> logger)
        {
            _service = service;
            _audit = audit;
            _config = config;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Ingest(
            [FromBody] IngestRiskEventRequest req,
            [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey,
            CancellationToken ct)
        {
            var expected = _config["InternalApi:Key"];
            if (string.IsNullOrEmpty(expected))
            {
                _logger.LogError("InternalApi:Key is not configured in AdminAPI");
                return StatusCode(500, new { message = "Server misconfiguration" });
            }
            if (string.IsNullOrEmpty(apiKey) || apiKey != expected)
                return Unauthorized(new { message = "Invalid or missing internal API key" });

            try
            {
                var result = await _service.IngestAsync(req, ct);

                // BR-249: block tự động của risk engine cũng phải có log (ai = system, cái gì, khi nào, lý do).
                if (result.Action == "BLOCK" && !result.IsShadow)
                {
                    try
                    {
                        await _audit.LogAsync(
                            userId: null,
                            actorType: "system",
                            action: "RISK_BLOCK_AUTO",
                            entityType: "RiskDecision",
                            entityId: result.DecisionCode,
                            newValue: new
                            {
                                targetUserId = req.UserId,
                                result.DecisionCodes,
                                result.Level,
                                result.Scopes,
                                result.BlockHours,
                                result.ExpiresAt,
                                result.ReasonCode,
                                result.FraudAlertId
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to write audit log for automatic block {Code}", result.DecisionCode);
                    }
                }
                return Ok(result);
            }
            catch (RiskServiceException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while ingesting risk event");
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }
    }
}