using System;
using System.Security.Claims;
using System.Threading.Tasks;
using CheckinAPI.DTOs;
using CheckinAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CheckinAPI.Controllers
{
    [ApiController]
    [Route("api/checkin")]
    public class OfflineCheckinController : ControllerBase
    {
        private readonly IOfflineCheckinService _offlineCheckinService;
        private readonly ILogger<OfflineCheckinController> _logger;

        public OfflineCheckinController(IOfflineCheckinService offlineCheckinService, ILogger<OfflineCheckinController> logger)
        {
            _offlineCheckinService = offlineCheckinService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? User.FindFirstValue("userId");
            return int.TryParse(value, out var id) ? id : 1;
        }

        private string GetCurrentUserName()
        {
            return User.Identity?.Name ?? User.FindFirstValue("name") ?? $"Staff #{GetCurrentUserId()}";
        }

        // ==========================================
        // UC_14.5 Enable Offline Check-in Mode: Preload Package
        // BR: Staff only downloads tickets for their assigned gate / ticket types
        // ==========================================
        [HttpGet("offline-package")]
        public async Task<IActionResult> GetOfflinePackage([FromQuery] int eventId = 1, [FromQuery] string? gateName = null)
        {
            try
            {
                var staffUserId = GetCurrentUserId();
                var result = await _offlineCheckinService.GetOfflinePackageAsync(eventId, gateName, staffUserId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offline ticket package for event {EventId}", eventId);
                return StatusCode(500, new { success = false, message = "Failed to load offline ticket package: " + ex.Message });
            }
        }

        // ==========================================
        // UC_14.6 Sync Offline Check-in Records
        // ==========================================
        [HttpPost("offline-sync")]
        public async Task<IActionResult> SyncOfflineScans([FromBody] OfflineSyncRequestDTO request)
        {
            try
            {
                var staffUserId = GetCurrentUserId();
                var result = await _offlineCheckinService.SyncOfflineScansAsync(request, staffUserId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing offline check-in scans for event {EventId}", request?.EventId);
                return StatusCode(500, new { success = false, message = "Failed to sync offline check-in scans: " + ex.Message });
            }
        }

        // ==========================================
        // UC_14.7 Resolve Offline Sync Conflicts
        // ==========================================
        [HttpGet("conflicts")]
        public async Task<IActionResult> GetOfflineConflicts([FromQuery] int? eventId, [FromQuery] string? status)
        {
            try
            {
                var result = await _offlineCheckinService.GetOfflineSyncConflictsAsync(eventId, status);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offline sync conflicts");
                return StatusCode(500, new { success = false, message = "Failed to load offline sync conflicts: " + ex.Message });
            }
        }

        [HttpPost("conflicts/{conflictId}/resolve")]
        public async Task<IActionResult> ResolveOfflineConflict(string conflictId, [FromBody] ResolveConflictRequestDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = GetCurrentUserName();
                var result = await _offlineCheckinService.ResolveOfflineConflictAsync(conflictId, request, userId, userName);
                return Ok(new { success = true, data = result, message = "Conflict resolution processed successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving offline sync conflict {ConflictId}", conflictId);
                return StatusCode(500, new { success = false, message = "Failed to resolve conflict: " + ex.Message });
            }
        }
    }
}
