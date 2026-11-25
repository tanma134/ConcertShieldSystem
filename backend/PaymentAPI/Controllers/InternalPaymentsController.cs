using Microsoft.AspNetCore.Mvc;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers
{
    [ApiController]
    [Route("api/internal/payments")]
    public class InternalPaymentsController : ControllerBase
    {
        private readonly IPaymentTransactionService _service;
        private readonly IConfiguration _config;

        public InternalPaymentsController(
            IPaymentTransactionService service,
            IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        [HttpPost("orders/{orderId:int}/cancel-pending")]
        public async Task<IActionResult> CancelPendingPayments(
            int orderId,
            CancellationToken ct)
        {
            var expected = _config["TicketApi:InternalApiKey"];
            var supplied = Request.Headers["X-Internal-Api-Key"].ToString();

            if (string.IsNullOrWhiteSpace(expected) ||
                !string.Equals(supplied, expected, StringComparison.Ordinal))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid service credentials."
                });
            }

            if (orderId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid order ID."
                });
            }

            var cancelledCount =
                await _service.CancelPendingPaymentsAsync(orderId);

            return Ok(new
            {
                success = true,
                orderId,
                cancelledCount
            });
        }
    }
}