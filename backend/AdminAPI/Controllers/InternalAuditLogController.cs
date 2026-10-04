using AdminAPI.DTOs;
using AdminAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    [ApiController]
    [Route("api/internal/audit-logs")]
    public class InternalAuditLogController : ControllerBase
    {
        private readonly IAuditLogService _service;
        private readonly IConfiguration _config;
        private readonly ILogger<InternalAuditLogController> _logger;

        public InternalAuditLogController(IAuditLogService service, IConfiguration config, ILogger<InternalAuditLogController> logger)
        {
            _service = service;
            _config = config;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateAuditLogRequest req,
            [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey)
        {
            var expected = _config["InternalApi:Key"];

            if (string.IsNullOrEmpty(expected))
            {
                _logger.LogError("InternalApi:Key is not configured in AdminAPI");
                return StatusCode(500, new { message = "Server misconfiguration" });
            }

            if (string.IsNullOrEmpty(apiKey) || apiKey != expected)
                return Unauthorized(new { message = "Invalid or missing internal API key" });

            if (string.IsNullOrWhiteSpace(req.Action) || string.IsNullOrWhiteSpace(req.EntityType))
                return BadRequest(new { message = "Action and EntityType are required" });

            try
            {
                await _service.LogAsync(
                    req.UserId,
                    req.ActorType,
                    req.Action,
                    req.EntityType,
                    req.EntityId,
                    req.OldValue,
                    req.NewValue,
                    req.IpAddress,
                    req.RequestId,
                    req.UserAgent);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while writing internal audit log");
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }
    }
}