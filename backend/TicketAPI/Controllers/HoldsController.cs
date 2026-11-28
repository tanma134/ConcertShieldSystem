using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketAPI.DTOs;
using TicketAPI.Risk;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class HoldsController : ControllerBase
    {
        private readonly IHoldService _holdService;
        private readonly TicketAPI.API.EventEligibilityClient _eligibility;
        private readonly TicketAPI.API.QueueAdmissionClient _queueAdmission;
        private readonly ITicketRiskService _risk;
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
            _risk = risk;
        }

        [EnableRateLimiting(TicketRateLimitPolicies.Hold)]
        [HttpPost("session")]
        public async Task<IActionResult> CreateHoldSession([FromBody] CreateHoldSessionRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid token."
                });

            // eKYC gate + fraud rules before any seat is reserved.
            var verdict = await _risk.CheckHoldAsync(HttpContext, userId.Value, request);
            if (!verdict.Allowed) return verdict.ToActionResult();

            try
            {
                var eligibility = await _eligibility.GetAsync(request.EventId, HttpContext.RequestAborted);
                if (!eligibility.CanSell)
                    throw new InvalidOperationException(eligibility.Reason);

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
                    "Failed to create hold session for user {UserId}, event {EventId}",
                    userId.Value,
                    request.EventId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while reserving tickets. Please try again."
                });
            }
        }

        [HttpGet("session/active")]
        public async Task<IActionResult> GetActiveHoldSession()
        {
            var userId = GetUserId();

            if (userId == null)
                return Unauthorized(new { success = false, message = "Invalid token." });

            try
            {
                var session = await _holdService.GetActiveHoldSessionAsync(userId.Value);

                return Ok(new
                {
                    success = true,
                    data = session
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to retrieve active hold session for user {UserId}",
                    userId.Value);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Could not check your active reservation."
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
                    message = "Invalid token."
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
                    "Failed to retrieve hold session {HoldId} for user {UserId}",
                    holdId,
                    userId.Value);

                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving the reservation session."
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
                return Unauthorized(new { success = false, message = "Invalid token." });

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

        [EnableRateLimiting(TicketRateLimitPolicies.Hold)]
        [HttpPost("session/{holdId}/cancel")]
        public async Task<IActionResult> CancelHoldSession(string holdId)
        {
            var userId = GetUserId();

            if (userId == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid token."
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
                    "Failed to cancel hold session {HoldId} for user {UserId}",
                    holdId,
                    userId.Value);

                return StatusCode(500, new
                {
                    success = false,
                    message = "Could not cancel the reservation."
                });
            }
        }

        [EnableRateLimiting(TicketRateLimitPolicies.Hold)]
        [HttpPut("session/{holdId}/seats")]
        public async Task<IActionResult> ChangeSeats(
            string holdId,
            [FromBody] ChangeHoldSeatsRequest request)
        {
            var userId = GetUserId();

            if (userId == null)
                return Unauthorized(new { success = false, message = "Invalid token." });

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
