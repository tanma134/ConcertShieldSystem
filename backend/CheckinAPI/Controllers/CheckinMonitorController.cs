using System;
using System.Security.Claims;
using System.Threading.Tasks;
using CheckinAPI.DTOs;
using CheckinAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CheckinAPI.Controllers
{
    [ApiController]
    [Route("api/checkin")]
    public class CheckinMonitorController : ControllerBase
    {
        private readonly ICheckinMonitorService _monitorService;
        private readonly ILogger<CheckinMonitorController> _logger;

        public CheckinMonitorController(ICheckinMonitorService monitorService, ILogger<CheckinMonitorController> logger)
        {
            _monitorService = monitorService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? User.FindFirstValue("userId");
            return int.TryParse(value, out var id) ? id : 0;
        }

        private bool IsAdminUser()
        {
            return User.IsInRole("Admin") || User.IsInRole("admin") || User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// UC_29 Monitor Real-time Check-in Status
        /// BR-395: 04 Real-time metrics (Total Issued, Checked-in, Pending, Occupancy Rate %)
        /// BR-396: Gate & Zone breakdown filtering
        /// BR-398 & BR-61: Access restriction to event owner (Organizer) and Admin
        /// </summary>
        [HttpGet("monitor")]
        public async Task<IActionResult> GetMonitorStatus([FromQuery] int eventId, [FromQuery] string? gateId, [FromQuery] string? zone)
        {
            if (eventId <= 0)
            {
                return BadRequest(new { message = "A valid eventId parameter is required." });
            }

            try
            {
                var userId = GetCurrentUserId();
                var isAdmin = IsAdminUser();

                var status = await _monitorService.GetMonitorStatusAsync(eventId, gateId, zone, userId, isAdmin);
                return Ok(new { success = true, data = status });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized check-in monitor access attempt (BR-398)");
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving check-in monitor status for event {EventId}", eventId);
                return StatusCode(500, new { success = false, message = "An unexpected server error occurred while retrieving check-in monitoring data." });
            }
        }

        /// <summary>
        /// UC_15.2 Scan Dynamic QR Ticket
        /// BR-08, BR-10, BR-152, BR-153, BR-289
        /// </summary>
        [HttpPost("scan-qr")]
        public async Task<IActionResult> ScanQrTicket([FromBody] ScanQrRequestDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = User.Identity?.Name ?? $"Staff #{userId}";
                var result = await _monitorService.ScanQrTicketAsync(request, userId, userName);
                return Ok(new { success = result.Success, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning QR ticket for event {EventId}", request.EventId);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred during QR scan validation." });
            }
        }

        /// <summary>
        /// UC_15.3 Manual Check-in
        /// BR-290, BR-292, BR-293
        /// </summary>
        [HttpPost("manual-checkin")]
        public async Task<IActionResult> ManualCheckin([FromBody] ManualCheckinRequestDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = User.Identity?.Name ?? $"Staff #{userId}";
                var result = await _monitorService.ManualCheckinAsync(request, userId, userName);
                return Ok(new { success = result.Success, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing manual check-in for ticket {TicketCode}", request.TicketCode);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred during manual check-in." });
            }
        }

        /// <summary>
        /// UC_15.4 Lookup Ticket
        /// BR-154, BR-155, BR-156, BR-291, BR-294 (PII Masking)
        /// </summary>
        [HttpGet("lookup-ticket")]
        public async Task<IActionResult> LookupTickets([FromQuery] int eventId, [FromQuery] string searchType, [FromQuery] string queryValue)
        {
            try
            {
                var userId = GetCurrentUserId();
                var request = new LookupTicketRequestDTO
                {
                    EventId = eventId,
                    SearchType = searchType ?? "PHONE",
                    QueryValue = queryValue ?? ""
                };
                var result = await _monitorService.LookupTicketsAsync(request, userId);
                return Ok(new { success = result.Success, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up tickets for event {EventId}", eventId);
                return StatusCode(500, new { success = false, message = "An error occurred during ticket search." });
            }
        }

        /// <summary>
        /// UC_15.5 View Check-in Scan History
        /// BR-295 (Staff view restricted to active shift entries)
        /// </summary>
        [HttpGet("scan-history")]
        public async Task<IActionResult> GetScanHistory([FromQuery] int eventId, [FromQuery] string? gateId, [FromQuery] string? statusFilter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _monitorService.GetScanHistoryAsync(eventId, gateId, statusFilter, userId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scan history for event {EventId}", eventId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching scan history." });
            }
        }

        /// <summary>
        /// UC_30 / UC-54 View Gate Assignment & Shift Info
        /// BR-399, BR-400
        /// </summary>
        [HttpGet("gate-assignment")]
        public async Task<IActionResult> GetGateAssignment([FromQuery] int eventId = 1)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _monitorService.GetGateAssignmentAsync(eventId, userId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving gate assignment for event {EventId}", eventId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching gate assignment." });
            }
        }
    }
}
