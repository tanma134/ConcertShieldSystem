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
        private const long MaxImageBytes = 6 * 1024 * 1024; // 3 ?nh x 6MB < gi?i h?n request 20MB
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

        /// <summary>N?i dung thông báo/ð?ng ? (phiên b?n ðang active trong DB) ð? frontend hi?n th? trý?c khi cho g?i ?nh.</summary>
        [HttpGet("consent")]
        public async Task<IActionResult> GetConsent()
        {
            try
            {
                return Ok(await _consentService.GetActiveAsync());
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Chýa c?u h?nh n?i dung ð?ng ? eKYC");
                return StatusCode(503, new { message = "N?i dung ð?ng ? chýa ðý?c c?u h?nh, vui l?ng th? l?i sau" });
            }
        }

        [HttpPost("submit")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Submit([FromForm] EkycSubmitRequestDto request)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Token không h?p l?" });

            // 1) Ph?i có ð?ng ? r? ràng, ðúng phiên b?n n?i dung hi?n hành
            if (!request.ConsentAccepted)
                return BadRequest(new { message = "B?n c?n ð?ng ? v?i n?i dung x? l? d? li?u cá nhân ð? ti?p t?c xác th?c" });

            string currentVersion;
            try
            {
                currentVersion = await _consentService.GetActiveVersionAsync();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Chýa c?u h?nh n?i dung ð?ng ? eKYC");
                return StatusCode(503, new { message = "N?i dung ð?ng ? chýa ðý?c c?u h?nh, vui l?ng th? l?i sau" });
            }

            if (request.ConsentVersion != currentVersion)
                return BadRequest(new { message = "N?i dung ð?ng ? ð? ðý?c c?p nh?t, vui l?ng ð?c l?i và xác nh?n" });

            // 2) Ki?m tra sõ b? file trý?c khi t?n lý?t g?i VNPT
            var fileError = ValidateImage(request.CccdFrontImage, "m?t trý?c CCCD")
                         ?? ValidateImage(request.CccdBackImage, "m?t sau CCCD")
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
                // 3 consecutive failures -> locked for 24 hours
                return StatusCode(423, new
                {
                    message = $"B?n ð? xác th?c th?t b?i 3 l?n liên ti?p. Vui l?ng th? l?i sau {ex.LockedUntilUtc:yyyy-MM-dd HH:mm} (UTC).",
                    lockedUntil = DateTime.SpecifyKind(ex.LockedUntilUtc, DateTimeKind.Utc)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "L?i khi x? l? submit eKYC cho user {UserId}", userId);
                return StatusCode(500, new { message = "Có l?i x?y ra khi x? l? xác th?c, vui l?ng th? l?i" });
            }
        }

        [HttpGet("status/{ekycId:int}")]
        public async Task<IActionResult> GetStatus(int ekycId)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Token không h?p l?" });

            try
            {
                var result = await _kycService.GetStatusAsync(ekycId, userId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Không t?m th?y b?n ghi eKYC" });
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
                return Unauthorized(new { message = "Token không h?p l?" });

            try
            {
                return Ok(await _accessLog.SearchMineAsync(userId, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "L?i khi ð?c nh?t k? truy c?p eKYC c?a user {UserId}", userId);
                return StatusCode(500, new { message = "Có l?i x?y ra, vui l?ng th? l?i" });
            }
        }

        private static string? ValidateImage(IFormFile? file, string name)
        {
            if (file is null || file.Length == 0)
                return $"Thi?u ?nh {name}";
            if (file.Length > MaxImageBytes)
                return $"?nh {name} quá l?n (t?i ða {MaxImageBytes / 1024 / 1024} MB)";
            if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                return $"?nh {name} ph?i là JPG ho?c PNG";
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