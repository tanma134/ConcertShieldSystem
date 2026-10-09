using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    // UC_14.3 / UC_14.4: staff assigned to one concert. Organizer of the concert (or Admin) only.
    [Route("api/events/{eventId:int}/staff")]
    [Authorize(Policy = "RequireOrganizer")]
    public class EventStaffController : BaseApiController
    {
        private readonly IEventStaffService _service;

        public EventStaffController(IEventStaffService service)
        {
            _service = service;
        }

        // UC_14.3: GET api/events/{eventId}/staff
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

        // UC_14.4: GET api/events/{eventId}/staff/candidates?q=an
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

        // UC_14.4: POST api/events/{eventId}/staff   { "staffUserId": 12 }
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

        // PUT api/events/{eventId}/staff/{staffUserId}/return-review   { "canReviewReturns": true }
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

        // UC_14.4: DELETE api/events/{eventId}/staff/{staffUserId}
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
