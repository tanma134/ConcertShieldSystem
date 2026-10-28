using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static TicketAPI.DTOs.AdminTicketDtos;
using TicketAPI.Services;
using TicketAPI.DTOs;

namespace TicketAPI.Controllers
{
    [Route("api/admin/tickets")]
    [ApiController]
    public class AdminTicketsController : TicketApiControllerBase
    {
        private readonly ITicketService _ticketService;

        public AdminTicketsController(ITicketService ticketService)
        {
            _ticketService = ticketService;
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
