using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.DTOs;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HoldsController : ControllerBase
    {
        private readonly IHoldService _holdService;
        private readonly ILogger<HoldsController> _logger;

        public HoldsController(
            IHoldService holdService,
            ILogger<HoldsController> logger)
        {
            _holdService = holdService;
            _logger = logger;
        }

        [HttpPost("session")]
        public async Task<IActionResult> CreateHoldSession([FromBody] CreateHoldSessionRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new
                {
                    success = false,
                    message = "Token không hợp lệ."
                });

            try
            {
                var session = await _holdService.CreateHoldSessionAsync(
                    userId.Value,
                    request,
                    durationSeconds: 600);

                return Ok(new
                {
                    success = true,
                    data = session
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Lỗi tạo hold session cho user {UserId}, event {EventId}",
                    userId.Value,
                    request.EventId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi khi giữ vé. Vui lòng thử lại."
                });
            }
        }

        [HttpGet("session/{holdId}")]
        public async Task<IActionResult> GetHoldDetails(string holdId)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new
                {
                    success = false,
                    message = "Token không hợp lệ."
                });

            try
            {
                var session = await _holdService.GetHoldDetailsAsync(
                    holdId,
                    userId.Value);

                return Ok(new
                {
                    success = true,
                    data = session
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Lỗi lấy hold session {HoldId} cho user {UserId}",
                    holdId,
                    userId.Value);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi khi lấy thông tin phiên giữ vé."
                });
            }
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                        ?? User.FindFirst("sub");

            return claim != null && int.TryParse(claim.Value, out var userId)
                ? userId
                : null;
        }

        [HttpPost("events/{eventId}/held-seats")]
        public async Task<IActionResult> GetHeldSeats(
            int eventId,
            [FromBody] List<int> seatIds)
        {
            if (eventId <= 0 || seatIds == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            var heldSeatIds = await _holdService.GetHeldSeatIdsAsync(eventId, seatIds);

            return Ok(new { success = true, data = heldSeatIds });
        }

        [HttpPut("session/{holdId}/attendees")]
        public async Task<IActionResult> SaveAttendees(string holdId, [FromBody] SaveHoldAttendeesRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new { success = false, message = "Token không hợp lệ." });

            try
            {
                await _holdService.SaveAttendeesAsync(
                    holdId,
                    userId.Value,
                    request.Attendees);

                return Ok(new { success = true });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
    }
}