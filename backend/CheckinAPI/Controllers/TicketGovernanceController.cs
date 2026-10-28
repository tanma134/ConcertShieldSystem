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
    public class TicketGovernanceController : ControllerBase
    {
        private readonly ITicketGovernanceService _governanceService;
        private readonly ILogger<TicketGovernanceController> _logger;

        public TicketGovernanceController(ITicketGovernanceService governanceService, ILogger<TicketGovernanceController> logger)
        {
            _governanceService = governanceService;
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
            return User.Identity?.Name ?? User.FindFirstValue("name") ?? $"Admin #{GetCurrentUserId()}";
        }

        private bool IsPlatformAdmin()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            if (!string.IsNullOrEmpty(role))
            {
                return role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || 
                       role.Equals("PlatformAdministrator", StringComparison.OrdinalIgnoreCase);
            }
            var userId = GetCurrentUserId();
            return userId > 0;
        }

        // ==========================================
        // UC_8.7 Bulk Revoke Tickets (event change)
        // ==========================================
        [HttpPost("governance/bulk-revoke")]
        public async Task<IActionResult> BulkRevokeTickets([FromBody] BulkRevokeRequestDTO request)
        {
            try
            {
                if (!IsPlatformAdmin())
                {
                    return StatusCode(403, new { success = false, message = "Access denied: Only Platform Administrators can execute bulk ticket revocation." });
                }

                if (request == null || request.EventId <= 0)
                {
                    return BadRequest(new { success = false, message = "EventId is required." });
                }

                if (string.IsNullOrWhiteSpace(request.ReasonNote))
                {
                    return BadRequest(new { success = false, message = "Revocation reason (ReasonNote) is strictly required." });
                }

                var adminId = GetCurrentUserId();
                var adminName = GetCurrentUserName();

                var result = await _governanceService.BulkRevokeTicketsAsync(request, adminId, adminName);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing bulk ticket revocation for event {EventId}", request?.EventId);
                return StatusCode(500, new { success = false, message = "Lỗi khi thu hồi vé hàng loạt: " + ex.Message });
            }
        }

        // ==========================================
        // UC_8.8 View Ticket Revocation History
        // ==========================================
        [HttpGet("governance/revocations")]
        public async Task<IActionResult> GetRevocationHistory(
            [FromQuery] int? eventId,
            [FromQuery] string? search,
            [FromQuery] string? reasonType,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _governanceService.GetRevocationHistoryAsync(eventId, search, reasonType, page, pageSize);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching revocation history");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải lịch sử thu hồi vé: " + ex.Message });
            }
        }

        // ==========================================
        // UC_8.10 View Reissue History (Traceability 1-1)
        // ==========================================
        [HttpGet("governance/reissues")]
        public async Task<IActionResult> GetReissueHistory(
            [FromQuery] int? eventId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _governanceService.GetReissueHistoryAsync(eventId, search, page, pageSize);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching reissue history");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải lịch sử cấp lại vé: " + ex.Message });
            }
        }

        // ==========================================
        // View Reconciliation Records for Bulk Revocations
        // ==========================================
        [HttpGet("governance/reconciliations")]
        public async Task<IActionResult> GetReconciliationRecords([FromQuery] int? eventId)
        {
            try
            {
                var result = await _governanceService.GetReconciliationRecordsAsync(eventId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching reconciliation records");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải biên bản đối soát: " + ex.Message });
            }
        }

        // ==========================================
        // Governance Dashboard Summary Stats
        // ==========================================
        [HttpGet("governance/stats")]
        public async Task<IActionResult> GetGovernanceStats()
        {
            try
            {
                var result = await _governanceService.GetGovernanceStatsAsync();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching governance stats");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải thống kê: " + ex.Message });
            }
        }

        // ==========================================
        // List Events for Governance Dropdown
        // ==========================================
        [HttpGet("governance/events")]
        public async Task<IActionResult> GetEventsForGovernance()
        {
            try
            {
                if (!IsPlatformAdmin())
                {
                    return StatusCode(403, new { success = false, message = "Access denied: Only Platform Administrators can access governance events." });
                }

                var result = await _governanceService.GetEventsForGovernanceAsync();
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching events for governance");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải danh sách sự kiện: " + ex.Message });
            }
        }

        // ==========================================
        // UC_14.5 Enable Offline Check-in Mode: Preload Package
        // ==========================================
        [HttpGet("offline-package")]
        public async Task<IActionResult> GetOfflinePackage([FromQuery] int eventId = 1)
        {
            try
            {
                var result = await _governanceService.GetOfflinePackageAsync(eventId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offline ticket package for event {EventId}", eventId);
                return StatusCode(500, new { success = false, message = "Lỗi khi tải gói dữ liệu vé ngoại tuyến: " + ex.Message });
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
                var result = await _governanceService.SyncOfflineScansAsync(request, staffUserId);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing offline check-in scans for event {EventId}", request?.EventId);
                return StatusCode(500, new { success = false, message = "Lỗi khi đồng bộ dữ liệu quét ngoại tuyến: " + ex.Message });
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
                var result = await _governanceService.GetOfflineSyncConflictsAsync(eventId, status);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offline sync conflicts");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải danh sách xung đột soát vé: " + ex.Message });
            }
        }

        [HttpPost("conflicts/{conflictId}/resolve")]
        public async Task<IActionResult> ResolveOfflineConflict(string conflictId, [FromBody] ResolveConflictRequestDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userName = GetCurrentUserName();
                var result = await _governanceService.ResolveOfflineConflictAsync(conflictId, request, userId, userName);
                return Ok(new { success = true, data = result, message = "Xử lý biên bản xung đột vé thành công." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving offline sync conflict {ConflictId}", conflictId);
                return StatusCode(500, new { success = false, message = "Lỗi khi xử lý xung đột: " + ex.Message });
            }
        }
    }
}
