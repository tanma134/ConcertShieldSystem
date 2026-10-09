using EventAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    // Service-to-service lookup used by TicketAPI: which events may this Staff account review returns for?
    // Protected by the shared internal API key, not by a user token.
    [ApiController]
    [Route("api/internal/staff/{staffUserId:int}/return-review-events")]
    public class InternalStaffController : ControllerBase
    {
        private readonly IEventStaffService _service;
        private readonly IConfiguration _config;

        public InternalStaffController(IEventStaffService service, IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> Get(int staffUserId)
        {
            var expected = _config["EventApi:InternalApiKey"];
            var supplied = Request.Headers["X-Internal-Api-Key"].ToString();
            if (string.IsNullOrWhiteSpace(expected) || !string.Equals(supplied, expected, StringComparison.Ordinal))
                return Unauthorized(new { success = false, message = "Invalid service credentials." });

            var ids = await _service.GetReturnReviewEventIdsAsync(staffUserId);
            return Ok(new { success = true, data = ids });
        }
    }
}
