using System.Security.Claims;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    /// <summary>3.4 Configure eKYC Consent Content (Admin). Nội dung đã tạo là bất biến; muốn đổi thì tạo phiên bản mới.</summary>
    [ApiController]
    [Route("api/admin/kyc/consent-versions")]
    [Authorize(Policy = "CanManageUsers")]
    public class KycConsentAdminController : ControllerBase
    {
        private readonly IKycConsentService _service;
        private readonly ILogger<KycConsentAdminController> _logger;

        public KycConsentAdminController(IKycConsentService service, ILogger<KycConsentAdminController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            try
            {
                return Ok(await _service.ListAsync());
            }
            catch (Exception ex)
            {
                return ServerError(ex, "listing consent versions");
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                return Ok(await _service.GetAsync(id));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ServerError(ex, "getting a consent version");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateKycConsentVersionDto dto)
        {
            try
            {
                return Ok(await _service.CreateAsync(dto, GetCurrentUserId()));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ServerError(ex, "creating a consent version");
            }
        }

        [HttpPost("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                await _service.ActivateAsync(id);
                return Ok(new { message = "Consent version activated. Users must consent again on their next submission." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ServerError(ex, "activating a consent version");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                return Ok(new { message = "Consent version deleted." });
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
                return ServerError(ex, "deleting a consent version");
            }
        }

        private int? GetCurrentUserId() =>
            int.TryParse((User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id) ? id : null;

        private IActionResult ServerError(Exception ex, string what)
        {
            _logger.LogError(ex, "Unexpected error while {What}", what);
            return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
        }
    }
}