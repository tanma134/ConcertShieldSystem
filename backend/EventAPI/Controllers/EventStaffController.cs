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

        // UC_14.4: POST api/events/{eventId}/staff   { "staffUserId": 12, "gateName": "Gate A", "shiftStartTime": "08:00", "shiftEndTime": "17:00" }
        [HttpPost]
        public async Task<ActionResult> Assign(int eventId, [FromBody] AssignStaffDTO dto)
        {
            try
            {
                var result = await _service.AssignAsync(
                    eventId,
                    dto.StaffUserId,
                    dto.GateName,
                    CurrentUserId,
                    IsAdmin,
                    BearerToken,
                    dto.CanReviewReturns,
                    dto.ShiftStartTime,
                    dto.ShiftEndTime,
                    dto.ShiftDate,
                    dto.CanManageIncidents,
                    dto.CanManageSupportCases,
                    dto.CanCheckin,
                    dto.ShiftEndDate);
                return Ok(ApiResponseDTO<EventStaffDTO>.SuccessResponse(result, "Staff member assigned."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // PUT api/events/{eventId}/staff/{staffUserId}/shift   { "gateName": "Gate A", "shiftStartTime": "08:00", "shiftEndTime": "17:00" }
        [HttpPut("{staffUserId:int}/shift")]
        public async Task<ActionResult> UpdateShift(int eventId, int staffUserId, [FromBody] UpdateStaffShiftDTO dto)
        {
            try
            {
                var result = await _service.UpdateShiftAsync(eventId, staffUserId, dto.GateName, dto.ShiftStartTime, dto.ShiftEndTime, CurrentUserId, IsAdmin, BearerToken, dto.ShiftDate, dto.ShiftEndDate);
                return Ok(ApiResponseDTO<EventStaffDTO>.SuccessResponse(result, "Staff shift assignment updated."));
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

        // PUT api/events/{eventId}/staff/{staffUserId}/duties   { "canReviewReturns": true, "canManageIncidents": true, "canManageSupportCases": false }
        [HttpPut("{staffUserId:int}/duties")]
        public async Task<ActionResult> UpdateDuties(int eventId, int staffUserId, [FromBody] UpdateStaffDutiesDTO dto)
        {
            try
            {
                var result = await _service.UpdateDutiesAsync(eventId, staffUserId, dto, CurrentUserId, IsAdmin, BearerToken);
                return Ok(ApiResponseDTO<EventStaffDTO>.SuccessResponse(result, "Staff duties updated."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // PUT api/events/{eventId}/staff/{staffUserId}/duty/{dutyType}?enabled=true
        [HttpPut("{staffUserId:int}/duty/{dutyType}")]
        public async Task<ActionResult> SetDuty(int eventId, int staffUserId, string dutyType, [FromQuery] bool enabled = true)
        {
            try
            {
                var result = await _service.SetDutyAsync(eventId, staffUserId, dutyType, enabled, CurrentUserId, IsAdmin, BearerToken);
                return Ok(ApiResponseDTO<EventStaffDTO>.SuccessResponse(result, $"Staff duty '{dutyType}' updated."));
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
