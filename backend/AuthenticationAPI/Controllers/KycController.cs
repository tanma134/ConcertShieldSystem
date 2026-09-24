using System.Security.Claims;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/kyc")]
    [Authorize]
    public class KycController : ControllerBase
    {
        private const long MaxImageBytes = 6 * 1024 * 1024; // 3 ảnh x 6MB < giới hạn request 20MB
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png" };

        private readonly IKycService _kycService;
        private readonly IKycConsentService _consentService;
        private readonly ILogger<KycController> _logger;

        public KycController(IKycService kycService, IKycConsentService consentService, ILogger<KycController> logger)
        {
            _kycService = kycService;
            _consentService = consentService;
            _logger = logger;
        }

        /// <summary>Nội dung thông báo/đồng ý (phiên bản đang active trong DB) để frontend hiển thị trước khi cho gửi ảnh.</summary>
        [HttpGet("consent")]
        public async Task<IActionResult> GetConsent()
        {
            try
            {
                return Ok(await _consentService.GetActiveAsync());
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Chưa cấu hình nội dung đồng ý eKYC");
                return StatusCode(503, new { message = "Nội dung đồng ý chưa được cấu hình, vui lòng thử lại sau" });
            }
        }

        [HttpPost("submit")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Submit([FromForm] EkycSubmitRequestDto request)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Token không hợp lệ" });

            // 1) Phải có đồng ý rõ ràng, đúng phiên bản nội dung hiện hành
            if (!request.ConsentAccepted)
                return BadRequest(new { message = "Bạn cần đồng ý với nội dung xử lý dữ liệu cá nhân để tiếp tục xác thực" });

            string currentVersion;
            try
            {
                currentVersion = await _consentService.GetActiveVersionAsync();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Chưa cấu hình nội dung đồng ý eKYC");
                return StatusCode(503, new { message = "Nội dung đồng ý chưa được cấu hình, vui lòng thử lại sau" });
            }

            if (request.ConsentVersion != currentVersion)
                return BadRequest(new { message = "Nội dung đồng ý đã được cập nhật, vui lòng đọc lại và xác nhận" });

            // 2) Kiểm tra sơ bộ file trước khi tốn lượt gọi VNPT
            var fileError = ValidateImage(request.CccdFrontImage, "mặt trước CCCD")
                         ?? ValidateImage(request.CccdBackImage, "mặt sau CCCD")
                         ?? ValidateImage(request.SelfieImage, "selfie");
            if (fileError is not null)
                return BadRequest(new { message = fileError });

            try
            {
                var result = await _kycService.SubmitAsync(request, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý submit eKYC cho user {UserId}", userId);
                return StatusCode(500, new { message = "Có lỗi xảy ra khi xử lý xác thực, vui lòng thử lại" });
            }
        }

        [HttpGet("status/{ekycId:int}")]
        public async Task<IActionResult> GetStatus(int ekycId)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { message = "Token không hợp lệ" });

            try
            {
                var result = await _kycService.GetStatusAsync(ekycId, userId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Không tìm thấy bản ghi eKYC" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        private static string? ValidateImage(IFormFile? file, string name)
        {
            if (file is null || file.Length == 0)
                return $"Thiếu ảnh {name}";
            if (file.Length > MaxImageBytes)
                return $"Ảnh {name} quá lớn (tối đa {MaxImageBytes / 1024 / 1024} MB)";
            if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                return $"Ảnh {name} phải là JPG hoặc PNG";
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