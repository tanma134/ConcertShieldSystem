using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    [Route("api/tickettypes")]
    public class TicketTypesController : BaseApiController
    {
        private readonly ITicketTypeService _ticketTypeService;
        private readonly IValidator<CreateTicketTypeDTO> _createValidator;

        public TicketTypesController(ITicketTypeService ticketTypeService, IValidator<CreateTicketTypeDTO> createValidator)
        {
            _ticketTypeService = ticketTypeService;
            _createValidator = createValidator;
        }

        [HttpGet("event/{eventId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            var result = await _ticketTypeService.GetByEventIdAsync(eventId);
            return Ok(ApiResponseDTO<List<TicketTypeResponseDTO>>.SuccessResponse(result));
        }

        [HttpPost("event/{eventId:int}")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> Create(int eventId, [FromBody] CreateTicketTypeDTO dto)
        {
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponseDTO<object>.FailResponse("Validation failed", validation.Errors.Select(e => e.ErrorMessage).ToList()));

            try
            {
                var result = await _ticketTypeService.CreateAsync(eventId, dto, CurrentUserId, IsAdmin);
                return CreatedAtAction(nameof(GetByEvent), new { eventId }, ApiResponseDTO<TicketTypeResponseDTO>.SuccessResponse(result, "Ticket type created"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTicketTypeDTO dto)
        {
            try
            {
                var result = await _ticketTypeService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<TicketTypeResponseDTO>.SuccessResponse(result, "Ticket type updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _ticketTypeService.DeleteAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Ticket type deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
