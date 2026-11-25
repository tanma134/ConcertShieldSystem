using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventAPI.Data;
using EventAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EventAPI.Controllers
{
    [ApiController]
    [Route("api/internal/governance")]
    public class InternalGovernanceController : ControllerBase
    {
        private readonly EventDbContext _db;
        private readonly IConfiguration _config;

        public InternalGovernanceController(EventDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        private bool IsAuthorized
        {
            get
            {
                var expected = _config["EventApi:InternalApiKey"] ?? _config["Governance:InternalApiKey"];
                var supplied = Request.Headers["X-Internal-Api-Key"].ToString();
                return !string.IsNullOrWhiteSpace(expected) && string.Equals(supplied, expected, System.StringComparison.Ordinal);
            }
        }

        [HttpGet("candidate-events")]
        public async Task<IActionResult> GetCandidateEvents(CancellationToken ct)
        {
            if (!IsAuthorized)
                return Unauthorized(new { success = false, message = "Invalid service credentials." });

            var approvedChangeEvents = await (from cr in _db.Set<EventChangeRequest>().AsNoTracking()
                                              where cr.Status == "Approved"
                                              select cr.EventId).Distinct().ToListAsync(ct);

            var events = await _db.Events.AsNoTracking()
                .Where(e => !e.IsDeleted && (
                    e.Status == "Cancelled" ||
                    e.Status == "Postponed" ||
                    e.Status == "Rescheduled" ||
                    approvedChangeEvents.Contains(e.EventId)))
                .OrderByDescending(e => e.EventId)
                .Take(100)
                .Select(e => new
                {
                    eventId = e.EventId,
                    title = e.Title,
                    status = e.Status,
                    startDate = e.StartsAt,
                    location = e.LocationName ?? e.Address ?? "Venue Center"
                })
                .ToListAsync(ct);

            return Ok(new { success = true, data = events });
        }
    }
}
