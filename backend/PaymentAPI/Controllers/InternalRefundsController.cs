using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers
{
    // Called by TicketAPI only (shared internal key), never by a browser.
    [ApiController]
    [Route("api/internal/refunds")]
    public class InternalRefundsController : ControllerBase
    {
        private readonly IRefundService _service;
        private readonly IConfiguration _config;

        public InternalRefundsController(IRefundService service, IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRefundRequestDto request, CancellationToken ct)
        {
            var expected = _config["TicketApi:InternalApiKey"];
            var supplied = Request.Headers["X-Internal-Api-Key"].ToString();
            if (string.IsNullOrWhiteSpace(expected) || !string.Equals(supplied, expected, StringComparison.Ordinal))
                return Unauthorized(new { success = false, message = "Invalid service credentials." });

            try
            {
                return Ok(await _service.RefundAsync(request, ct));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
