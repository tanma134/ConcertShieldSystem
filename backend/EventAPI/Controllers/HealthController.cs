using EventAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly EventDbContext _context;

        public HealthController(EventDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                bool canConnect = await _context.Database.CanConnectAsync();
                int eventCount = await _context.Events.CountAsync();

                return Ok(new
                {
                    Status = "Healthy",
                    Service = "EventAPI",
                    Database = "PostgreSQL (ConcertShield)",
                    Connected = canConnect,
                    EventCount = eventCount,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Service = "EventAPI",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }
}
