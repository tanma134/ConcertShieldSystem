using System.Security.Claims;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    /// <summary>3.5 Manage eKYC Data Retention Policy (Admin).</summary>
    [ApiController]
    [Route("api/admin/kyc/settings")]
    [Authorize(Policy = "CanManageUsers")]
    public class KycSettingsAdminController : ControllerBase
    {
        private readonly IKycSettingService _service;
        private readonly ILogger<KycSettingsAdminController> _logger;

        public KycSettingsAdminController(IKycSettingService service, ILogger<KycSettingsAdminController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                return Ok(await _service.GetAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while reading KYC settings");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateKycSettingDto dto)
        {
            try
            {
                var adminId = int.TryParse((User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
                    ? id
                    : (int?)null;
                return Ok(await _service.UpdateAsync(dto, adminId));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating KYC settings");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }
    }
}