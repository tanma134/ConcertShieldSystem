using System.Security.Claims;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/kyc/deletion-requests")]
    [Authorize]
    public class KycDeletionController : ControllerBase
    {
        private readonly IKycDeletionService _service;
        private readonly ILogger<KycDeletionController> _logger;

        public KycDeletionController(IKycDeletionService service, ILogger<KycDeletionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // POST api/kyc/deletion-requests
        [HttpPost]
        public async Task<IActionResult> Request()
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Invalid token" });

            try
            {
                var result = await _service.RequestAsync(userId, HttpContext.Connection.RemoteIpAddress?.ToString());
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating KYC deletion request for user {UserId}", userId);
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }

        // GET api/kyc/deletion-requests/me
        [HttpGet("me")]
        public async Task<IActionResult> GetMine()
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Invalid token" });

            try
            {
                return Ok(await _service.GetMineAsync(userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while listing KYC deletion requests for user {UserId}", userId);
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return claim is not null && int.TryParse(claim.Value, out userId);
        }
    }

    /// <summary>3.6 Request eKYC Data Deletion - phía Admin xử lý.</summary>
    [ApiController]
    [Route("api/admin/kyc/deletion-requests")]
    [Authorize(Policy = "CanManageUsers")]
    public class KycDeletionAdminController : ControllerBase
    {
        private readonly IKycDeletionService _service;
        private readonly ILogger<KycDeletionAdminController> _logger;

        public KycDeletionAdminController(IKycDeletionService service, ILogger<KycDeletionAdminController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET api/admin/kyc/deletion-requests?status=Pending&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                return Ok(await _service.ListAsync(status, page, pageSize));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ServerError(ex, "listing KYC deletion requests");
            }
        }

        // POST api/admin/kyc/deletion-requests/5/approve
        [HttpPost("{id:int}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                return Ok(await _service.ApproveAsync(id, GetCurrentUserId(), GetClientIp()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ServerError(ex, "approving a KYC deletion request");
            }
        }

        // POST api/admin/kyc/deletion-requests/5/reject   body: { "note": "..." }
        [HttpPost("{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectKycDeletionDto dto)
        {
            try
            {
                return Ok(await _service.RejectAsync(id, GetCurrentUserId(), dto.Note, GetClientIp()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ServerError(ex, "rejecting a KYC deletion request");
            }
        }

        private int? GetCurrentUserId() =>
            int.TryParse((User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id) ? id : null;

        private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private IActionResult ServerError(Exception ex, string what)
        {
            _logger.LogError(ex, "Unexpected error while {What}", what);
            return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
        }
    }
}