using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    /// <summary>
    /// Dynamic pricing rules layered on a TicketType's base price (UC_27.1 — Configure
    /// Pricing Rule): time-based discounts (EarlyBird/LastMinute), fixed calendar
    /// windows (TimeBased), or quantity-triggered price steps (QuantityBased).
    ///
    /// Configuration only — resolving a rule into a live sale price at checkout is
    /// TicketAPI's responsibility. Rules are only editable while the concert is Draft
    /// or Rejected, same as ticket types and refund policies.
    /// </summary>
    [Route("api/pricingrules")]
    public class PricingRulesController : BaseApiController
    {
        private readonly IPricingRuleService _pricingRuleService;
        private readonly IValidator<CreatePricingRuleDTO> _createValidator;

        public PricingRulesController(
            IPricingRuleService pricingRuleService,
            IValidator<CreatePricingRuleDTO> createValidator)
        {
            _pricingRuleService = pricingRuleService;
            _createValidator = createValidator;
        }

        /// <summary>All pricing rules configured for one ticket type, highest priority first.</summary>
        [HttpGet("tickettype/{ticketTypeId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByTicketType(int ticketTypeId)
        {
            try
            {
                var result = await _pricingRuleService.GetByTicketTypeIdAsync(ticketTypeId, CurrentUserIdOrNull, IsAdmin);
                return Ok(ApiResponseDTO<List<PricingRuleResponseDTO>>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Create([FromBody] CreatePricingRuleDTO dto)
        {
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponseDTO<object>.FailResponse(
                    "Validation failed", validation.Errors.Select(e => e.ErrorMessage).ToList()));

            try
            {
                var result = await _pricingRuleService.CreateAsync(dto.TicketTypeId, dto, CurrentUserId, IsAdmin);
                return CreatedAtAction(nameof(GetByTicketType), new { ticketTypeId = dto.TicketTypeId },
                    ApiResponseDTO<PricingRuleResponseDTO>.SuccessResponse(result, "Pricing rule created"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePricingRuleDTO dto)
        {
            try
            {
                var result = await _pricingRuleService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<PricingRuleResponseDTO>.SuccessResponse(result, "Pricing rule updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _pricingRuleService.DeleteAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Pricing rule deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
