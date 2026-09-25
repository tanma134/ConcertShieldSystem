using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
        [Route("api/seating")]
    public class SeatingController : BaseApiController
    {
        private readonly ISeatingService _seatingService;

        public SeatingController(ISeatingService seatingService)
        {
            _seatingService = seatingService;
        }
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
        [HttpGet("zones/{seatZoneId:int}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetZoneSeats(int seatZoneId)
        {
            try
            {
                var result = await _seatingService.GetZoneSeatsAsync(seatZoneId, CurrentUserIdOrNull, IsAdmin);
                return Ok(ApiResponseDTO<SeatZoneResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
        [HttpPost("event/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Build(int eventId, [FromBody] BuildSeatingChartDTO dto)
        {
            try
            {
                var result = await _seatingService.BuildAsync(eventId, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatingChartResponseDTO>.SuccessResponse(
                    result,
                    $"Seating chart built: {result.Zones.Count} zone(s), {result.Zones.Sum(z => z.TotalSeats)} seat(s)."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
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
        [HttpPut("zones/{seatZoneId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> UpdateZone(int seatZoneId, [FromBody] UpdateSeatZoneDTO dto)
        {
            try
            {
                var result = await _seatingService.UpdateZoneAsync(seatZoneId, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatZoneResponseDTO>.SuccessResponse(result, "Zone updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
        [HttpDelete("zones/{seatZoneId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> DeleteZone(int seatZoneId)
        {
            try
            {
                await _seatingService.DeleteZoneAsync(seatZoneId, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Zone deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
        [HttpDelete("event/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int eventId)
        {
            try
            {
                await _seatingService.DeleteAsync(eventId, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(
                    null!, "Seating chart deleted. The concert is now general admission."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
