using System.Security.Claims;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    /// <summary>
    /// Admin side of eKYC: review requests waiting for manual review (BR-109) and view their images (BR-55).
    /// Every image view is logged (table kyc_access_logs).
    /// </summary>
    [ApiController]
    [Route("api/admin/kyc")]
    [Authorize(Policy = "CanManageUsers")]
    public class KycAdminController : ControllerBase
    {
        private readonly IEkycRepository _repository;
        private readonly IObjectStorage _storage;
        private readonly IKycAccessLogService _accessLog;
        private readonly IKycReviewService _review;
        private readonly ILogger<KycAdminController> _logger;

        public KycAdminController(IEkycRepository repository, IObjectStorage storage,
                                  IKycAccessLogService accessLog, IKycReviewService review,
                                  ILogger<KycAdminController> logger)
        {
            _repository = repository;
            _storage = storage;
            _accessLog = accessLog;
            _review = review;
            _logger = logger;
        }

        // GET api/admin/kyc/reviews?page=1&pageSize=20
        [HttpGet("reviews")]
        public async Task<IActionResult> ListReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                return Ok(await _review.ListPendingAsync(page, pageSize));
            }
            catch (Exception ex)
            {
                return ServerError(ex, "listing eKYC reviews");
            }
        }

        // POST api/admin/kyc/reviews/7/approve
        [HttpPost("reviews/{ekycId:int}/approve")]
        public async Task<IActionResult> Approve(int ekycId)
        {
            var adminId = GetCurrentUserId();
            if (adminId is null) return Unauthorized(new { message = "Invalid token" });

            try
            {
                await _review.ApproveAsync(ekycId, adminId.Value, GetClientIp());
                return Ok(new { message = "eKYC request approved" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return ServerError(ex, "approving an eKYC request"); }
        }

        // POST api/admin/kyc/reviews/7/reject   body: { "reason": "..." }
        [HttpPost("reviews/{ekycId:int}/reject")]
        public async Task<IActionResult> Reject(int ekycId, [FromBody] RejectKycReviewDto dto)
        {
            var adminId = GetCurrentUserId();
            if (adminId is null) return Unauthorized(new { message = "Invalid token" });

            try
            {
                await _review.RejectAsync(ekycId, adminId.Value, dto.Reason, GetClientIp());
                return Ok(new { message = "eKYC request rejected" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return ServerError(ex, "rejecting an eKYC request"); }
        }

        // PUT api/admin/kyc/7/legal-hold   body: { "hold": true, "reason": "..." }
        [HttpPut("{ekycId:int}/legal-hold")]
        public async Task<IActionResult> SetLegalHold(int ekycId, [FromBody] SetKycLegalHoldDto dto)
        {
            var adminId = GetCurrentUserId();
            if (adminId is null) return Unauthorized(new { message = "Invalid token" });

            try
            {
                await _review.SetLegalHoldAsync(ekycId, dto.Hold, dto.Reason, adminId.Value, GetClientIp());
                return Ok(new { message = dto.Hold ? "Legal hold placed" : "Legal hold cleared" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { return ServerError(ex, "changing a legal hold"); }
        }

        // GET api/admin/kyc/7/images/front|back|selfie
        [HttpGet("{ekycId:int}/images/{kind}")]
        public async Task<IActionResult> GetImage(int ekycId, string kind)
        {
            var record = await _repository.GetByIdAsync(ekycId);
            if (record is null)
                return NotFound();

            // BR-55: images may be viewed only to review a request that is waiting for review
            if (record.Status != "ManualReview")
                return StatusCode(403, new { message = "Images can only be viewed while the request is waiting for review" });

            var normalizedKind = kind.ToLowerInvariant();
            var key = normalizedKind switch
            {
                "front" => record.CccdFrontObjectKey,
                "back" => record.CccdBackObjectKey,
                "selfie" => record.FaceCaptureObjectKey,
                _ => null
            };
            if (key is null)
                return NotFound();

            var stream = await _storage.DownloadAsync(key);
            if (stream is null)
                return NotFound();

            var adminId = GetCurrentUserId();
            var ip = GetClientIp();

            // Ghi log TRƯỚC khi trả ảnh: không ghi được log thì không cho xem
            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                KycAccessActions.ViewImage, ip, $"purpose=manual_review; kind={normalizedKind}");
            _logger.LogWarning("KYC image access: admin {AdminId} xem {Kind} của ekyc {EkycId} (user {UserId})",
                adminId, normalizedKind, ekycId, record.UserId);

            Response.Headers["Cache-Control"] = "no-store";
            return File(stream, "image/jpeg");
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
