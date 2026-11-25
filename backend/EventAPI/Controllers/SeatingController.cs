using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
// Quan ly so do ghe ngoi va khu vuc cho concert danh cho Organizer va Guest
    [Route("api/seating")]
    public class SeatingController : BaseApiController
    {
        private readonly ISeatingService _seatingService;

        public SeatingController(ISeatingService seatingService)
        {
            _seatingService = seatingService;
        }

        // Lay so do ghe chi tiet day du gom cac khu vuc va ghe cua concert danh cho nguoi dung
        [HttpGet("event/{eventId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            try
            {
                var result = await _seatingService.GetByEventIdAsync(eventId, CurrentUserIdOrNull, IsAdmin);

                if (result == null)
                    return Ok(ApiResponseDTO<SeatingChartResponseDTO?>.SuccessResponse(
                        null, "This concert has no seating chart (general admission)."));

                return Ok(ApiResponseDTO<SeatingChartResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Xem truoc tong quan so luong ghe theo tung khu vuc danh cho Guest va Organizer
        [HttpGet("event/{eventId:int}/preview")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPreview(int eventId)
        {
            try
            {
                var result = await _seatingService.GetPreviewAsync(eventId, CurrentUserIdOrNull, IsAdmin);

                if (result == null)
                    return Ok(ApiResponseDTO<SeatingChartPreviewDTO?>.SuccessResponse(
                        null, "This concert has no seating chart (general admission)."));

                return Ok(ApiResponseDTO<SeatingChartPreviewDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Lay danh sach cac ghe con trong trong mot khu vuc danh cho khach hang chon ve
        [HttpGet("zones/{seatZoneId:int}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetZoneSeats(int seatZoneId, [FromQuery] bool availableOnly = false)
        {
            try
            {
                // Luu y: Endpoint nay chi doc trang thai ghe de hien thi, khong khoa hay giu cho
                var result = await _seatingService.GetZoneSeatsAsync(seatZoneId, CurrentUserIdOrNull, IsAdmin, availableOnly);
                return Ok(ApiResponseDTO<SeatZoneResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Khoi tao hoac ve lai toan bo so do ghe ngoi cua concert danh cho Organizer
        [HttpPost("event/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Build(int eventId, [FromBody] BuildSeatingChartDTO dto)
        {
            try
            {
                // Luu y: Moi zone phai gan voi mot hang ve thuoc concert va concert phai o trang thai Draft/Rejected
                var result = await _seatingService.BuildAsync(eventId, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatingChartResponseDTO>.SuccessResponse(
                    result,
                    $"Seating chart built: {result.Zones.Count} zone(s), {result.Zones.Sum(z => z.TotalSeats)} seat(s)."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Them mot khu vuc ghe moi vao so do da co san danh cho Organizer
        [HttpPost("event/{eventId:int}/zones")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> AddZone(int eventId, [FromBody] CreateSeatZoneDTO dto)
        {
            try
            {
                var result = await _seatingService.AddZoneAsync(eventId, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatZoneResponseDTO>.SuccessResponse(
                    result, $"Zone '{result.ZoneName}' added with {result.TotalSeats} seat(s)."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Cap nhat thong tin hinh hoc va hang ve lien ket cua khu vuc ghe danh cho Organizer
        [HttpPut("zones/{seatZoneId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> UpdateZone(int seatZoneId, [FromBody] UpdateSeatZoneDTO dto)
        {
            try
            {
                // Luu y: Tu choi doi hang ve neu da co bat ky ghe nao trong khu vuc duoc giu hoac ban
                var result = await _seatingService.UpdateZoneAsync(seatZoneId, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatZoneResponseDTO>.SuccessResponse(result, "Zone updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Cap nhat metadata bo cuc san khau va kich thuoc canvas danh cho Organizer
        [HttpPut("event/{eventId:int}/layout")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> UpdateLayout(int eventId, [FromBody] UpdateLayoutDTO dto)
        {
            try
            {
                var result = await _seatingService.UpdateLayoutAsync(eventId, dto.LayoutJson ?? "{}", CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatingChartResponseDTO>.SuccessResponse(result, "Layout updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Xoa mot khu vuc ghe khoi so do danh cho Organizer
        [HttpDelete("zones/{seatZoneId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> DeleteZone(int seatZoneId)
        {
            try
            {
                // Luu y: Khong the xoa khu vuc neu da co ve duoc dat hoac ban
                await _seatingService.DeleteZoneAsync(seatZoneId, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Zone deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Xoa toan bo so do ghe va dua concert ve dang ve tu do (General Admission) danh cho Organizer
        [HttpDelete("event/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int eventId)
        {
            try
            {
                // Luu y: Chi duoc phep xoa khi concert chua phat sinh bat ky luot giu ve nao
                await _seatingService.DeleteAsync(eventId, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(
                    null!, "Seating chart deleted. The concert is now general admission."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
