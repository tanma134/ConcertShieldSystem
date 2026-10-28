using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Route("api/admin/status-histories")]
    public class AdminStatusHistoriesController : ControllerBase
    {
        private readonly IStatusHistoryService _statusHistoryService;

        public AdminStatusHistoriesController(
            IStatusHistoryService statusHistoryService)
        {
            _statusHistoryService = statusHistoryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? eventId,
            [FromQuery] string? targetType,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _statusHistoryService.GetAdminHistoryAsync(
                eventId,
                targetType,
                from,
                to,
                page,
                pageSize);

            return Ok(new
            {
                success = true,
                data = result
            });
        }
    }
}
