using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    // Quan ly cac hang ve cua concert gom thiet lap gia, so luong va han muc dat ve
    [Route("api/tickettypes")]
    public class TicketTypesController : BaseApiController
    {
        private readonly ITicketTypeService _ticketTypeService;
        private readonly IValidator<CreateTicketTypeDTO> _createValidator;
        private readonly IValidator<UpdateTicketTypeDTO> _updateValidator;

        public TicketTypesController(
            ITicketTypeService ticketTypeService,
            IValidator<CreateTicketTypeDTO> createValidator,
            IValidator<UpdateTicketTypeDTO> updateValidator)
        {
            _ticketTypeService = ticketTypeService;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
        }

        // Lay danh sach cac hang ve da cau hinh cua concert
        [HttpGet("event/{eventId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            try
            {
                var result = await _ticketTypeService.GetByEventIdAsync(eventId, CurrentUserIdOrNull, IsAdmin);
                return Ok(ApiResponseDTO<List<TicketTypeResponseDTO>>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Tao moi mot hang ve cho concert o trang thai Draft hoac Rejected
        [HttpPost("event/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Create(int eventId, [FromBody] CreateTicketTypeDTO dto)
        {
            // Luu y: Validate du lieu hang ve va gioi han dat ve MinPerOrder, MaxPerOrder
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

        // Cap nhat thong tin hang ve nhu ten, gia, so luong hoac gioi han mua
        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTicketTypeDTO dto)
        {
            // Luu y: Kiem tra khop id giua route va body truoc khi cap nhat
            if (dto.TicketTypeId != 0 && dto.TicketTypeId != id)
                return BadRequest(ApiResponseDTO<object>.FailResponse("Route id and body TicketTypeId do not match."));

            var validation = await _updateValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponseDTO<object>.FailResponse("Validation failed", validation.Errors.Select(e => e.ErrorMessage).ToList()));

            try
            {
                var result = await _ticketTypeService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<TicketTypeResponseDTO>.SuccessResponse(result, "Ticket type updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Xoa hang ve khoi concert khi chua phat sinh ve ban ra
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // Luu y: Chi cho phep xoa khi concert chua ban ve va khong co vung ghe su dung
                await _ticketTypeService.DeleteAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Ticket type deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
