using System.Net;
using AdminAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Controllers
{
    /// <summary>
    /// Endpoint NỘI BỘ: TicketAPI / AuthenticationAPI hỏi "user / IP / thiết bị / session này đang bị block thật không?".
    /// Chỉ tính block THẬT (không phải Shadow), còn hiệu lực và chưa Restore, cộng với blacklist.
    /// Bảo vệ bằng X-Internal-Api-Key. KHÔNG public route này ra Gateway cho browser.
    /// </summary>
    [ApiController]
    [Route("api/internal/risk")]
    public class InternalRiskCheckController : ControllerBase
    {
        private readonly AdminDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<InternalRiskCheckController> _logger;

        public InternalRiskCheckController(AdminDbContext db, IConfiguration config, ILogger<InternalRiskCheckController> logger)
        { _db = db; _config = config; _logger = logger; }

        // GET api/internal/risk/check?userId=1&ip=1.2.3.4&deviceFp=abc&sessionId=xyz
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
                var hasSession = !string.IsNullOrWhiteSpace(sessionId);
                var now = DateTime.UtcNow;

                // 1) Block do engine hoặc Admin tạo, còn hiệu lực, chưa Restore, không phải Shadow
                var block = await _db.RiskDecisions.AsNoTracking()
                    .Where(d => d.Action == "BLOCK" && d.Status == "Active" && !d.IsShadow
                                && (d.ExpiresAt == null || d.ExpiresAt > now)
                                && ((userId != null && d.Scope == "ACCOUNT" && d.UserId == userId)
                                    || (hasDevice && d.Scope == "DEVICE" && d.DeviceFp == deviceFp)
                                    || (ipAddr != null && d.Scope == "IP" && d.IpAddress == ipAddr)
                                    || (hasSession && d.Scope == "SESSION" && d.SessionId == sessionId)))
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new { d.Scope, d.ExpiresAt, d.ReasonPublic })
                    .FirstOrDefaultAsync(ct);

                if (block != null)
                    return Ok(new
                    {
                        blocked = true,
                        scope = block.Scope,
                        expiresAt = block.ExpiresAt,
                        message = block.ReasonPublic ?? "Your account or device is temporarily restricted due to unusual activity."
                    });

                // 2) Blacklist cứng (HR_BL02 / HR_BL03 và USER)
                var values = new List<string>();
                if (userId.HasValue) values.Add(userId.Value.ToString());
                if (hasDevice) values.Add(deviceFp!);
                if (ipAddr != null) values.Add(ipAddr.ToString());

                if (values.Count > 0)
                {
                    var black = await _db.RiskLists.AsNoTracking().AnyAsync(l =>
                        l.ListType == "BLACKLIST" && (l.ExpiresAt == null || l.ExpiresAt > now)
                        && values.Contains(l.ValueHash)
                        && (l.ValueType == "USER" || l.ValueType == "DEVICE" || l.ValueType == "IP"), ct);
                    if (black)
                        return Ok(new
                        {
                            blocked = true,
                            scope = "BLACKLIST",
                            expiresAt = (DateTime?)null,
                            message = "Your account or device is restricted. Please contact support."
                        });
                }

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
