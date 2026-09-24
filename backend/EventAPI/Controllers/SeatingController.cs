using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    /// <summary>
    /// Assigned-seating configuration for a concert.
    ///
    /// A concert is general admission by default (HasSeatingChart = false, no rows here).
    /// Building a chart flips it to assigned seating and generates one Seat row per
    /// physical seat; deleting the chart flips it back.
    ///
    /// Charts are only editable while the concert is Draft or Rejected.
    /// </summary>
    [Route("api/seating")]
    public class SeatingController : BaseApiController
    {
        private readonly ISeatingService _seatingService;

        public SeatingController(ISeatingService seatingService)
        {
            _seatingService = seatingService;
        }

        /// <summary>
        /// Full chart with every zone and seat. Returns 204 when the concert is
        /// general admission (no chart configured).
        /// </summary>
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

        /// <summary>Zone-level summary with seat counts — no per-seat list.</summary>
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

        /// <summary>
        /// The seats of one Seated zone — this is what the buyer's seat picker reads.
        /// Pass availableOnly=true to get only seats that are still free.
        /// Returns 400 for a Standing zone, which has no individual seats.
        ///
        /// NOTE: holding and selling a seat belongs to TicketAPI/QueueAPI. This
        /// endpoint is read-only; it never changes seat status.
        /// </summary>
        [HttpGet("zones/{seatZoneId:int}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetZoneSeats(int seatZoneId, [FromQuery] bool availableOnly = false)
        {
            try
            {
                var result = await _seatingService.GetZoneSeatsAsync(seatZoneId, CurrentUserIdOrNull, IsAdmin, availableOnly);
                return Ok(ApiResponseDTO<SeatZoneResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        /// <summary>
        /// Creates or replaces the concert's seating chart and generates all seats.
        /// Each zone must reference a TicketType belonging to this concert.
        /// Sets HasSeatingChart = true.
        /// </summary>
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

        /// <summary>Adds one more zone (and its seats) to an existing chart.</summary>
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

        /// <summary>
        /// Renames a zone, changes its shape, or re-links it to another ticket type.
        /// Re-linking is refused once any seat in the zone is held/reserved/sold.
        /// </summary>
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

        /// <summary>Deletes a zone and all of its seats. Refused if any seat is taken.</summary>
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

        /// <summary>
        /// Deletes the whole chart and switches the concert back to general
        /// admission (HasSeatingChart = false).
        /// </summary>
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
