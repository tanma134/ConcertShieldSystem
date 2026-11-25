using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    // Quan ly cac quy tac gia dong ap dung cho tung hang ve nhu EarlyBird, LastMinute
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

        // Lay danh sach cac quy tac gia dong ap dung cho mot hang ve
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

        // Tao quy tac gia dong nhu EarlyBird hoac LastMinute cho hang ve
        [HttpPost]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Create([FromBody] CreatePricingRuleDTO dto)
        {
            // Luu y: Validate du lieu quy tac gia dong truoc khi them moi
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

        // Cap nhat thong tin quy tac gia dong cua hang ve
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

        // Xoa quy tac gia dong khoi hang ve
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
