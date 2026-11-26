using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static TicketAPI.DTOs.AdminTicketDtos;
using TicketAPI.Services;
using TicketAPI.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace TicketAPI.Controllers
{
    [Route("api/admin/tickets")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminTicketsController : TicketApiControllerBase
    {
        private readonly ITicketService _ticketService;

        private readonly IAuditLogClient _auditLog;

        public AdminTicketsController(ITicketService ticketService, IAuditLogClient auditLog)
        {
            _ticketService = ticketService;
            _auditLog = auditLog;
        }

        [HttpGet]
        public async Task<IActionResult> GetTickets([FromQuery] AdminTicketQueryDto query)
        {
            try
            {
                var result = await _ticketService.GetAdminTicketsAsync(query);
                return Success(result);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        [HttpGet("{ticketId}")]
        public async Task<IActionResult> GetTicketDetails(int ticketId)
        {
            try
            {
                var ticket =
                    await _ticketService.GetAdminTicketDetailsAsync(ticketId);

                if (ticket == null)
                    return NotFound(new
                    {
                        success = false,
                        message = "Ticket not found."
                    });

                return Success(ticket);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        [HttpPost("{ticketId:int}/reissue")]
        public async Task<IActionResult> ReissueTicket(
            int ticketId,
            [FromBody] ReissueTicketRequestDto request)
        {
            try
            {
                var replacement = await _ticketService.ReissueTicketAsync(
                    ticketId,
                    CurrentUserId,
                    request.Reason);

                await _auditLog.LogAsync(CurrentUserId, AuditActorType, "TICKET_REISSUED", "Ticket", ticketId.ToString(),
                    newValue: new { reason = request.Reason }, ipAddress: ClientIp);

                return Success(
                    replacement,
                    "Replacement ticket issued successfully.");
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        [HttpPost("{ticketId:int}/revoke")]
        public async Task<IActionResult> RevokeSingle(
            int ticketId,
            [FromBody] RevokeTicketRequestDto request)
        {
            try
            {
                await _ticketService.RevokeSingleAsync(
                    ticketId,
                    CurrentUserId,
                    request.Reason);

                await _auditLog.LogAsync(CurrentUserId, AuditActorType, "TICKET_REVOKED", "Ticket", ticketId.ToString(),
                    newValue: new { status = "Revoked", reason = request.Reason }, ipAddress: ClientIp);

                return Success(
                    new { ticketId, status = "Revoked" },
                    "Ticket revoked successfully.");
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }
    }
}
