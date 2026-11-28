using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    [Route("api/refundpolicies")]
    public class RefundPoliciesController : BaseApiController
    {
        private readonly IRefundPolicyService _refundPolicyService;
        private readonly IValidator<CreateRefundPolicyDTO> _createValidator;
        private readonly IAuditLogClient _audit;

        public RefundPoliciesController(
            IRefundPolicyService refundPolicyService,
            IValidator<CreateRefundPolicyDTO> createValidator,
            IAuditLogClient audit)
        {
            _refundPolicyService = refundPolicyService;
            _createValidator = createValidator;
            _audit = audit;
        }

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

        [HttpPost("event/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Create(int eventId, [FromBody] CreateRefundPolicyDTO dto)
        {
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponseDTO<object>.FailResponse(
                    "Validation failed", validation.Errors.Select(e => e.ErrorMessage).ToList()));

            try
            {
                var result = await _refundPolicyService.CreateAsync(eventId, dto, CurrentUserId, IsAdmin);
                await AuditAsync(_audit, "REFUND_POLICY_CREATED", "RefundPolicy", result.RefundPolicyId);
                return CreatedAtAction(nameof(GetByEvent), new { eventId },
                    ApiResponseDTO<RefundPolicyResponseDTO>.SuccessResponse(result, "Refund policy created"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateRefundPolicyDTO dto)
        {
            try
            {
                var result = await _refundPolicyService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                await AuditAsync(_audit, "REFUND_POLICY_UPDATED", "RefundPolicy", id);
                return Ok(ApiResponseDTO<RefundPolicyResponseDTO>.SuccessResponse(result, "Refund policy updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _refundPolicyService.DeleteAsync(id, CurrentUserId, IsAdmin);
                await AuditAsync(_audit, "REFUND_POLICY_DELETED", "RefundPolicy", id);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Refund policy deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
