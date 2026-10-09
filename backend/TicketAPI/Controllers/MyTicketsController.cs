using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    // "My tickets": the signed-in customer's paid tickets and their return state.
    [Route("api/tickets/my")]
    [Authorize]
    public class MyTicketsController : TicketApiControllerBase
    {
        private readonly ITicketReturnService _service;
        private readonly ILogger<MyTicketsController> _logger;

        public MyTicketsController(ITicketReturnService service, ILogger<MyTicketsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET api/tickets/my
        [HttpGet]
        public async Task<IActionResult> GetMyTickets()
        {
            try
            {
                return Success(await _service.ListMyTicketsAsync(CurrentUserId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not load tickets for user {UserId}", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value);
                return Fail(ex);
            }
        }
    }
}
