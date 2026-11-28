using System.Security.Claims;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/kyc")]
    [Authorize(Policy = "Customer")] // only a Customer can use eKYC
    public class KycController : ControllerBase
    {
        private const long MaxImageBytes = 6 * 1024 * 1024; // 3 images x 6MB < 20MB request limit
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png" };

        private readonly IKycService _kycService;
        private readonly IKycConsentService _consentService;
        private readonly IKycAccessLogService _accessLog;
        private readonly ILogger<KycController> _logger;

        public KycController(IKycService kycService, IKycConsentService consentService,
                             IKycAccessLogService accessLog, ILogger<KycController> logger)
        {
            _kycService = kycService;
            _consentService = consentService;
            _accessLog = accessLog;
            _logger = logger;
        }

        /// <summary>Consent content (the version currently active in the DB) for the frontend to show before allowing image upload.</summary>
        [HttpGet("consent")]
        public async Task<IActionResult> GetConsent()
        {
            try
            {
                return Ok(await _consentService.GetActiveAsync());
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "eKYC consent content is not configured");
                return StatusCode(503, new { message = "Consent content is not configured, please try again later" });
            }
        }

        [HttpPost("submit")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Submit([FromForm] EkycSubmitRequestDto request)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Invalid token" });

            // 1) Explicit consent is required, and it must match the current content version
            if (!request.ConsentAccepted)
                return BadRequest(new { message = "You must agree to the personal data processing terms to continue verification" });

            string currentVersion;
            try
            {
                currentVersion = await _consentService.GetActiveVersionAsync();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "eKYC consent content is not configured");
                return StatusCode(503, new { message = "Consent content is not configured, please try again later" });
            }

            if (request.ConsentVersion != currentVersion)
                return BadRequest(new { message = "The consent content has been updated, please review and confirm again" });

            // 2) Quick file check before spending a VNPT call
            var fileError = ValidateImage(request.CccdFrontImage, "front side of the ID card")
                         ?? ValidateImage(request.CccdBackImage, "back side of the ID card")
                         ?? ValidateImage(request.SelfieImage, "selfie");
            if (fileError is not null)
                return BadRequest(new { message = fileError });

            try
            {
                var result = await _kycService.SubmitAsync(request, userId, HttpContext.Connection.RemoteIpAddress?.ToString());
                return Ok(result);
            }
            catch (EkycLockedException ex)
            {
              
                return StatusCode(423, new
                {
                    message = $"Verification failed 3 times in a row. Please try again after {ex.LockedUntilUtc:yyyy-MM-dd HH:mm} (UTC).",
                    lockedUntil = DateTime.SpecifyKind(ex.LockedUntilUtc, DateTimeKind.Utc)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing eKYC submit for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while processing the verification, please try again" });
            }
        }

        [HttpGet("status/{ekycId:int}")]
        public async Task<IActionResult> GetStatus(int ekycId)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Invalid token" });

            try
            {
                var result = await _kycService.GetStatusAsync(ekycId, userId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "eKYC record not found" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary> the user can see who accessed their eKYC data and for what purpose.</summary>
        [HttpGet("access-logs/me")]
        public async Task<IActionResult> GetMyAccessLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Invalid token" });

            try
            {
                return Ok(await _accessLog.SearchMineAsync(userId, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading the eKYC access log for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred, please try again" });
            }
        }

        private static string? ValidateImage(IFormFile? file, string name)
        {
            if (file is null || file.Length == 0)
                return $"Missing {name} image";
            if (file.Length > MaxImageBytes)
                return $"The {name} image is too large (max {MaxImageBytes / 1024 / 1024} MB)";
            if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                return $"The {name} image must be JPG or PNG";
            return null;
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return claim is not null && int.TryParse(claim.Value, out userId);
        }
    }
}