using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    [Route("api/events/{eventId:int}/staff")]
    [Authorize(Policy = "RequireOrganizer")]
    public class EventStaffController : BaseApiController
    {
        private readonly IEventStaffService _service;

        public EventStaffController(IEventStaffService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult> GetStaff(int eventId)
        {
            try
            {
                var result = await _service.ListAsync(eventId, CurrentUserId, IsAdmin, BearerToken);
                return Ok(ApiResponseDTO<List<EventStaffDTO>>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("candidates")]
        public async Task<ActionResult> GetCandidates(int eventId, [FromQuery] string? q)
        {
            try
            {
                var result = await _service.SearchCandidatesAsync(eventId, CurrentUserId, IsAdmin, BearerToken, q);
                return Ok(ApiResponseDTO<List<StaffCandidateDTO>>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost]
        public async Task<ActionResult> Assign(int eventId, [FromBody] AssignStaffDTO dto)
        {
            try
            {
                var result = await _service.AssignAsync(eventId, dto.StaffUserId, dto.GateName, CurrentUserId, IsAdmin, BearerToken, dto.CanReviewReturns);
                return Ok(ApiResponseDTO<EventStaffDTO>.SuccessResponse(result, "Staff member assigned."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{staffUserId:int}/return-review")]
        public async Task<ActionResult> SetReturnReview(int eventId, int staffUserId, [FromBody] SetReturnReviewDTO dto)
        {
            try
            {
                var result = await _service.SetReturnReviewAsync(eventId, staffUserId, dto.CanReviewReturns, CurrentUserId, IsAdmin, BearerToken);
                return Ok(ApiResponseDTO<EventStaffDTO>.SuccessResponse(result, "Return review duty updated."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{staffUserId:int}")]
        public async Task<ActionResult> Unassign(int eventId, int staffUserId)
        {
            try
            {
                await _service.UnassignAsync(eventId, staffUserId, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(new { eventId, staffUserId }, "Staff member removed."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
