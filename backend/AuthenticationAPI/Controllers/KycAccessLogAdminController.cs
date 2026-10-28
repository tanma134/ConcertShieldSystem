using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    /// <summary>3.7 View eKYC Data Access Log (Admin, chỉ đọc).</summary>
    [ApiController]
    [Route("api/admin/kyc/access-logs")]
    [Authorize(Policy = "CanManageUsers")]
    public class KycAccessLogAdminController : ControllerBase
    {
        private readonly IKycAccessLogService _service;
        private readonly ILogger<KycAccessLogAdminController> _logger;

        public KycAccessLogAdminController(IKycAccessLogService service, ILogger<KycAccessLogAdminController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET api/admin/kyc/access-logs?subjectUserId=5&actorType=Admin&action=VIEW_IMAGE&from=2026-09-01&to=2026-09-30&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] KycAccessLogQuery query)
        {
            try
            {
                return Ok(await _service.SearchAsync(query));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while searching KYC access logs");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }
    }
}