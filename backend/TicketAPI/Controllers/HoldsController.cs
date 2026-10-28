using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.DTOs;
using TicketAPI.Risk;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HoldsController : ControllerBase
    {
        private readonly IHoldService _holdService;
        private readonly TicketAPI.API.EventEligibilityClient _eligibility;
        private readonly TicketAPI.API.QueueAdmissionClient _queueAdmission;
        private readonly ILogger<HoldsController> _logger;

        public HoldsController(
            IHoldService holdService,
            ILogger<HoldsController> logger, TicketAPI.API.EventEligibilityClient eligibility,
            TicketAPI.API.QueueAdmissionClient queueAdmission, ITicketRiskService risk)
        {
            _holdService = holdService;
            _logger = logger;
            _eligibility = eligibility;
            _queueAdmission = queueAdmission;
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
                var eligibility = await _eligibility.GetAsync(request.EventId, HttpContext.RequestAborted);
                if (!eligibility.CanSell)
                    throw new InvalidOperationException(eligibility.Reason);

                if (eligibility.RequiresVirtualQueue)
                {
                    await _queueAdmission.EnsureAdmittedAsync(
                        request.EventId,
                        Request.Headers.Authorization,
                        Request.Headers["X-Queue-Token"],
                        HttpContext.RequestAborted);
                }

                var session = await _holdService.CreateHoldSessionAsync(
                    userId.Value,
                    request);

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
            catch (TicketAPI.API.QueueAdmissionRequiredException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (TicketAPI.API.QueueApiUnavailableException ex)
            {
                _logger.LogError(ex, "Queue admission validation failed for user {UserId}, event {EventId}", userId.Value, request.EventId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
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

        [HttpPost("session/{holdId}/cancel")]
        public async Task<IActionResult> CancelHoldSession(string holdId)
        {
            var userId = GetUserId();

            if (userId == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Token không hợp lệ."
                });
            }

            try
            {
                await _holdService.CancelHoldSessionAsync(holdId, userId.Value);

                return Ok(new
                {
                    success = true,
                    message = "Reservation cancelled and tickets released."
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
                    "Lỗi hủy hold session {HoldId} cho user {UserId}",
                    holdId,
                    userId.Value);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Could not cancel the reservation."
                });
            }
        }

        [HttpPut("session/{holdId}/seats")]
        public async Task<IActionResult> ChangeSeats(
            string holdId,
            [FromBody] ChangeHoldSeatsRequest request)
        {
            var userId = GetUserId();

            if (userId == null)
                return Unauthorized(new { success = false, message = "Token không hợp lệ." });

            try
            {
                var result = await _holdService.ChangeSeatsAsync(
                    holdId,
                    userId.Value,
                    request.SeatIds);

                return Ok(new { success = true, data = result });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
        }
    }
}