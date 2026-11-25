using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    // Quan ly cac chinh sach hoan ve cua concert theo cac moc thoi gian truoc gio dien
    [Route("api/refundpolicies")]
    public class RefundPoliciesController : BaseApiController
    {
        private readonly IRefundPolicyService _refundPolicyService;
        private readonly IValidator<CreateRefundPolicyDTO> _createValidator;

        public RefundPoliciesController(
            IRefundPolicyService refundPolicyService,
            IValidator<CreateRefundPolicyDTO> createValidator)
        {
            _refundPolicyService = refundPolicyService;
            _createValidator = createValidator;
        }

        // Lay danh sach cac chinh sach hoan ve da cau hinh cua concert
        [HttpGet("event/{eventId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            try
            {
                var result = await _refundPolicyService.GetByEventIdAsync(eventId, CurrentUserIdOrNull, IsAdmin);
                return Ok(ApiResponseDTO<List<RefundPolicyResponseDTO>>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Tao moi chinh sach hoan ve voi moc thoi gian va phan tram hoan
        [HttpPost("event/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Create(int eventId, [FromBody] CreateRefundPolicyDTO dto)
        {
            // Luu y: Validate du lieu chinh sach hoan ve truoc khi luu
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponseDTO<object>.FailResponse(
                    "Validation failed", validation.Errors.Select(e => e.ErrorMessage).ToList()));

            try
            {
                var result = await _refundPolicyService.CreateAsync(eventId, dto, CurrentUserId, IsAdmin);
                return CreatedAtAction(nameof(GetByEvent), new { eventId },
                    ApiResponseDTO<RefundPolicyResponseDTO>.SuccessResponse(result, "Refund policy created"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Cap nhat moc thoi gian va ty le hoan ve cho su kien
        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateRefundPolicyDTO dto)
        {
            try
            {
                // Luu y: Chi sua duoc khi concert o Draft hoac Rejected de dam bao hop dong nguoi mua
                var result = await _refundPolicyService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<RefundPolicyResponseDTO>.SuccessResponse(result, "Refund policy updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Xoa chinh sach hoan ve khoi concert
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _refundPolicyService.DeleteAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Refund policy deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
