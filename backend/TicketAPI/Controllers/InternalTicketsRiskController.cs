using Microsoft.AspNetCore.Mvc;
using TicketAPI.Services;

namespace TicketAPI.Controllers;

// Internal endpoints called by AdminAPI risk engine, protected with X-Internal-Api-Key.
[ApiController]
[Route("api/internal/tickets/risk")]
public class InternalTicketsRiskController : ControllerBase
{
    private readonly IRiskTicketAdminService _service;
    private readonly IConfiguration _config;
    private readonly IAuditLogClient _auditLog;

    public InternalTicketsRiskController(IRiskTicketAdminService service, IConfiguration config, IAuditLogClient auditLog)
    {
        _auditLog = auditLog;
        _service = service;
        _config = config;
    }

    private bool Allowed
    {
        get
        {
            var expected = _config["Governance:InternalApiKey"] ?? _config["AdminApi:InternalKey"];
            return !string.IsNullOrEmpty(expected)
                   && Request.Headers["X-Internal-Api-Key"] == expected;
        }
    }

    [HttpPost("{ticketId:int}/block")]
    public async Task<IActionResult> Block(int ticketId, [FromBody] RiskTicketActionInput input, CancellationToken ct)
    {
        if (!Allowed) return Unauthorized();
        try
        {
            await _service.BlockAsync(ticketId, input.UserId, input.Reason ?? "Blocked by risk review", input.StaffId, ct);
            await _auditLog.LogAsync(input.StaffId, "system", "TICKET_BLOCKED", "Ticket", ticketId.ToString(),
                newValue: new { status = "Blocked", ownerUserId = input.UserId, reason = input.Reason, expiresAt = input.ExpiresAt });
            return Ok(new { success = true, ticketId, status = "Blocked", expiresAt = input.ExpiresAt });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{ticketId:int}/unblock")]
    public async Task<IActionResult> Unblock(int ticketId, [FromBody] RiskTicketActionInput input, CancellationToken ct)
    {
        if (!Allowed) return Unauthorized();
        try
        {
            await _service.UnblockAsync(ticketId, input.UserId, input.Reason ?? "Unblocked by risk review", input.StaffId, ct);
            await _auditLog.LogAsync(input.StaffId, "system", "TICKET_UNBLOCKED", "Ticket", ticketId.ToString(),
                oldValue: new { status = "Blocked" }, newValue: new { status = "Unblocked", ownerUserId = input.UserId, reason = input.Reason });
            return Ok(new { success = true, ticketId, status = "Unblocked" });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{ticketId:int}/revoke-and-refund")]
    public async Task<IActionResult> RevokeAndRefund(int ticketId, [FromBody] RiskTicketRevokeInput input, CancellationToken ct)
    {
        if (!Allowed) return Unauthorized();
        try
        {
            var result = await _service.RevokeAndRefundAsync(
                ticketId, input.UserId, input.AppealId, input.StaffId,
                input.Reason ?? "Risk appeal rejected", ct);

            await _auditLog.LogAsync(input.StaffId, "system", "TICKET_REVOKED_REFUNDED", "Ticket", ticketId.ToString(),
                newValue: new { status = result.Status, ownerUserId = input.UserId, appealId = input.AppealId, refundStatus = result.RefundStatus, refundAmount = result.RefundAmount });

            return Ok(new
            {
                success = true,
                ticketRevoked = result.TicketRevoked,
                status = result.Status,
                refundStatus = result.RefundStatus,
                refundAmount = result.RefundAmount,
                refundReference = result.RefundReference,
                refundError = result.RefundError,
                returnRequestId = result.ReturnRequestId
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    // Lets AdminAPI confirm the ticket exists and belongs to the user before
    // creating a manual TICKET block.
    [HttpGet("{ticketId:int}")]
    public async Task<IActionResult> Get(int ticketId, [FromQuery] int? userId, CancellationToken ct)
    {
        if (!Allowed) return Unauthorized();
        var ticket = await _service.FindTicketAsync(ticketId, ct);
        if (ticket == null) return NotFound(new { message = "Ticket not found." });

        if (userId.HasValue && ticket.OwnerUserId != userId.Value)
            return Conflict(new { message = "This ticket does not belong to that user." });

        return Ok(new
        {
            success = true,
            ticketId = ticket.TicketId,
            ownerUserId = ticket.OwnerUserId,
            eventId = ticket.EventId,
            status = ticket.Status,
            checkedInAt = ticket.CheckedInAt
        });
    }

    public sealed class RiskTicketActionInput
    {
        public int UserId { get; set; }
        public int StaffId { get; set; }
        public string? Reason { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public sealed class RiskTicketRevokeInput
    {
        public int UserId { get; set; }
        public int AppealId { get; set; }
        public int StaffId { get; set; }
        public string? Reason { get; set; }
    }
}
