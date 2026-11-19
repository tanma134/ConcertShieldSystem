using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    // Quan ly nhan vien ho tro va phan cong soat ve tai concert danh cho Organizer va Admin
    [Route("api/events/{eventId:int}/staff")]
    [Authorize(Policy = "RequireOrganizer")]
    public class EventStaffController : BaseApiController
    {
        private readonly IEventStaffService _service;

        public EventStaffController(IEventStaffService service)
        {
            _service = service;
        }

        // Lay danh sach nhan vien duoc phan cong lam viec tai su kien
        [HttpGet]
        public async Task<ActionResult> GetStaff(int eventId)
        {
            try
            {
                // Luu y: Kiem tra quyen Organizer hoac Admin truoc khi lay danh sach nhan vien
                var result = await _service.ListAsync(eventId, CurrentUserId, IsAdmin, BearerToken);
                return Ok(ApiResponseDTO<List<EventStaffDTO>>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Tim kiem nhan vien ung vien tu danh ba he thong de phan cong vao su kien
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

        // Phan cong mot nhan vien vao lam viec tai concert kem cong soat ve
        [HttpPost]
        public async Task<ActionResult> Assign(int eventId, [FromBody] AssignStaffDTO dto)
        {
            try
            {
                // Luu y: Kiem tra nhan vien hop le tu Directory va khong duoc phan cong trung lap
                var result = await _service.AssignAsync(eventId, dto.StaffUserId, dto.GateName, CurrentUserId, IsAdmin, BearerToken, dto.CanReviewReturns);
                return Ok(ApiResponseDTO<EventStaffDTO>.SuccessResponse(result, "Staff member assigned."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Cap nhat quyen duyet hoan ve cho nhan vien tai su kien
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

        // Huy phan cong nhan vien khoi concert
        [HttpDelete("{staffUserId:int}")]
        public async Task<ActionResult> Unassign(int eventId, int staffUserId)
        {
            try
            {
                // Luu y: Xoa quyen soat ve va truy cap cua nhan vien vao su kien nay
                await _service.UnassignAsync(eventId, staffUserId, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(new { eventId, staffUserId }, "Staff member removed."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
