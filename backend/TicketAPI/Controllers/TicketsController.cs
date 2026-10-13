using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketsController(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpGet("my-tickets")]
        public async Task<IActionResult> GetMyTickets()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                        ?? User.FindFirst("sub");

            if (claim == null || !int.TryParse(claim.Value, out var userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Token không hợp lệ."
                });
            }

            var tickets = await _ticketService.GetMyTicketsAsync(userId);

            return Ok(new
            {
                success = true,
                data = tickets
            });
        }

        [HttpGet("{ticketId}")]
        public async Task<IActionResult> GetMyTicketDetails(int ticketId)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                        ?? User.FindFirst("sub");

            if (claim == null || !int.TryParse(claim.Value, out var userId))
                return Unauthorized(new { success = false, message = "Invalid token." });

            var ticket = await _ticketService.GetMyTicketDetailsAsync(ticketId, userId);

            if (ticket == null)
                return NotFound(new { success = false, message = "Ticket not found." });

            return Ok(new { success = true, data = ticket });
        }
    }
}
