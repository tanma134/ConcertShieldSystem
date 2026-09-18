using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    /// <summary>
    /// Refund terms for a concert. A concert may have several tiers, e.g.
    /// "100% refund up to 168h before" plus "50% refund up to 48h before".
    ///
    /// Terms are part of what buyers agree to, so they're only editable while the
    /// concert is Draft or Rejected.
    /// </summary>
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

        /// <summary>Active refund policies for a concert, longest deadline first.</summary>
        [HttpGet("event/{eventId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            var result = await _refundPolicyService.GetByEventIdAsync(eventId);
            return Ok(ApiResponseDTO<List<RefundPolicyResponseDTO>>.SuccessResponse(result));
        }

        [HttpPost("event/{eventId:int}")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> Create(int eventId, [FromBody] CreateRefundPolicyDTO dto)
        {
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

        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateRefundPolicyDTO dto)
        {
            try
            {
                var result = await _refundPolicyService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<RefundPolicyResponseDTO>.SuccessResponse(result, "Refund policy updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireCustomer")]
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
