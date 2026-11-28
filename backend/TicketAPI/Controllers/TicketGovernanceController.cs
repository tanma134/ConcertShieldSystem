using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TicketAPI.DTOs;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    // Bulk revocation cancels tickets for a whole event, so it must never be reachable
    // by a customer or an anonymous caller. The only screen that calls it is the Admin
    // ticket-governance page.
    [ApiController]
    [Route("api/tickets/governance")]
    [Authorize(Roles = "Admin")]
    public class TicketGovernanceController : TicketApiControllerBase
    {
        private readonly ITicketGovernanceService _governanceService;
        private readonly ILogger<TicketGovernanceController> _logger;
        private readonly IAuditLogClient _auditLog;

        public TicketGovernanceController(
            ITicketGovernanceService governanceService,
            ILogger<TicketGovernanceController> logger,
            IAuditLogClient auditLog)
        {
            _auditLog = auditLog;
            _governanceService = governanceService;
            _logger = logger;
        }

        private string CurrentUserName =>
            User.Identity?.Name ?? User.FindFirstValue("name") ?? $"Admin #{CurrentUserId}";

        // ==========================================
        // UC_8.7 Bulk Revoke Tickets (Event Change)
        // ==========================================
        [HttpPost("bulk-revoke")]
        public async Task<IActionResult> BulkRevokeTickets([FromBody] BulkRevokeRequestDTO request)
        {
            try
            {
                var adminId = CurrentUserId;
                var adminName = CurrentUserName;

                var result = await _governanceService.BulkRevokeTicketsAsync(request, adminId, adminName);

                await _auditLog.LogAsync(adminId, AuditActorType, "TICKET_BULK_REVOKED", "Event", request?.EventId.ToString(),
                    newValue: new { eventId = request?.EventId, message = result.Message }, ipAddress: ClientIp);

                return Success(result, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing bulk ticket revocation for event {EventId}", request?.EventId);
                return Fail(ex);
            }
        }

        // ==========================================
        // UC_8.8 View Ticket Revocation History
        // ==========================================
        [HttpGet("revocations")]
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
                return Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching revocation history");
                return Fail(ex);
            }
        }

        // ==========================================
        // UC_8.10 View Reissue History (Traceability 1-1)
        // ==========================================
        [HttpGet("reissues")]
        public async Task<IActionResult> GetReissueHistory(
            [FromQuery] int? eventId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _governanceService.GetReissueHistoryAsync(eventId, search, page, pageSize);
                return Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching reissue history");
                return Fail(ex);
            }
        }

        // ==========================================
        // View Reconciliation Records for Bulk Revocations
        // ==========================================
        [HttpGet("reconciliations")]
        public async Task<IActionResult> GetReconciliationRecords([FromQuery] int? eventId)
        {
            try
            {
                var result = await _governanceService.GetReconciliationRecordsAsync(eventId);
                return Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching reconciliation records");
                return Fail(ex);
            }
        }

        // ==========================================
        // Governance Summary Stats
        // ==========================================
        [HttpGet("stats")]
        public async Task<IActionResult> GetGovernanceStats()
        {
            try
            {
                var result = await _governanceService.GetGovernanceStatsAsync();
                return Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching governance stats");
                return Fail(ex);
            }
        }

        // ==========================================
        // List Events for Governance Dropdown
        // ==========================================
        [HttpGet("events")]
        public async Task<IActionResult> GetEventsForGovernance()
        {
            try
            {
                var result = await _governanceService.GetEventsForGovernanceAsync();
                return Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching events for governance");
                return Fail(ex);
            }
        }
    }
}
