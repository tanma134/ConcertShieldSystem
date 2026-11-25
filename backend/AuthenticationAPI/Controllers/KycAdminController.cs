using System.Security.Claims;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    /// <summary>
    /// Admin side of eKYC: review requests waiting for manual review  and view their images .
    /// Every image view is logged (table kyc_access_logs).
    /// </summary>
    [ApiController]
    [Route("api/admin/kyc")]
    [Authorize(Policy = "CanManageUsers")]
    public class KycAdminController : ControllerBase
    {
        private readonly IKycReviewService _review;
        private readonly ILogger<KycAdminController> _logger;

        public KycAdminController(IKycReviewService review, ILogger<KycAdminController> logger)
        {
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
            try
            {
                var result = await _review.GetReviewImageAsync(ekycId, kind, GetCurrentUserId(), GetClientIp());
                return result.Outcome switch
                {
                    KycImageOutcome.Ok => ImageFile(result.Content!),
                    KycImageOutcome.NotInReview => StatusCode(403, new { message = "Images can only be viewed while the request is waiting for review" }),
                    _ => NotFound()
                };
            }
            catch (Exception ex)
            {
                return ServerError(ex, "viewing an eKYC image");
            }
        }

        // GET api/admin/kyc/{ekycId}/evidence/front|back|selfie?reason=...
        // Evidence access for cases that are no longer waiting for review (appeals,
        // authority requests, re-checking an approved submission). The reason is
        // mandatory and every view is logged, so the customer can see who looked.
        [HttpGet("{ekycId:int}/evidence/{kind}")]
        public async Task<IActionResult> GetEvidence(int ekycId, string kind, [FromQuery] string? reason)
        {
            var adminId = GetCurrentUserId();
            if (adminId is null) return Unauthorized(new { message = "Invalid token" });

            reason = (reason ?? "").Trim();
            if (reason.Length < 10 || reason.Length > 300)
                return BadRequest(new { message = "A reason of 10-300 characters is required to view eKYC evidence" });

            try
            {
                var result = await _review.GetEvidenceImageAsync(ekycId, kind, reason, adminId, GetClientIp());
                return result.Outcome switch
                {
                    KycImageOutcome.Ok => ImageFile(result.Content!),
                    KycImageOutcome.Purged => StatusCode(410, new { message = "The data of this record has already been deleted" }),
                    _ => NotFound()
                };
            }
            catch (Exception ex)
            {
                return ServerError(ex, "viewing eKYC evidence");
            }
        }

        private IActionResult ImageFile(Stream content)
        {
            Response.Headers["Cache-Control"] = "no-store";
            return File(content, "image/jpeg");
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
