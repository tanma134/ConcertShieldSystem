using System.Net;
using AdminAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    // Internal endpoint (protected by X-Internal-Api-Key, never exposed via gateway):
    // checks active non-shadow blocks and blacklists.
    [ApiController]
    [Route("api/internal/risk")]
    public class InternalRiskCheckController : ControllerBase
    {
        private readonly IRiskDecisionRepository _decisions;
        private readonly IRiskListRepository _lists;
        private readonly IConfiguration _config;
        private readonly ILogger<InternalRiskCheckController> _logger;

        public InternalRiskCheckController(IRiskDecisionRepository decisions, IRiskListRepository lists,
            IConfiguration config, ILogger<InternalRiskCheckController> logger)
        { _decisions = decisions; _lists = lists; _config = config; _logger = logger; }

        [HttpGet("check")]
        public async Task<IActionResult> Check(
            [FromQuery] int? userId, [FromQuery] string? ip, [FromQuery] string? deviceFp, [FromQuery] string? sessionId,
            [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey, CancellationToken ct)
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
                IPAddress? ipAddr = null;
                if (!string.IsNullOrWhiteSpace(ip) && !IPAddress.TryParse(ip, out ipAddr))
                    return BadRequest(new { message = "ip is invalid." });

                var hasDevice = !string.IsNullOrWhiteSpace(deviceFp);
                var now = DateTime.UtcNow;

                // 1) Block do engine hoặc Admin tạo, còn hiệu lực, chưa Restore, không phải Shadow
                var block = await _decisions.FindActiveBlockAsync(userId, ipAddr, deviceFp, sessionId, now, ct);

                if (block != null)
                    return Ok(new
                    {
                        blocked = true,
                        scope = block.Scope,
                        expiresAt = block.ExpiresAt,
                        message = block.ReasonPublic ?? "Your account or device is temporarily restricted due to unusual activity."
                    });

                // Hard blacklists (HR_BL02 / HR_BL03 and USER)
                var values = new List<string>();
                if (userId.HasValue) values.Add(userId.Value.ToString());
                if (hasDevice) values.Add(deviceFp!);
                if (ipAddr != null) values.Add(ipAddr.ToString());

                if (await _lists.HasActiveBlacklistEntryAsync(values, now, ct))
                    return Ok(new
                    {
                        blocked = true,
                        scope = "BLACKLIST",
                        expiresAt = (DateTime?)null,
                        message = "Your account or device is restricted. Please contact support."
                    });

                return Ok(new { blocked = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking risk block");
                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }
    }
}
