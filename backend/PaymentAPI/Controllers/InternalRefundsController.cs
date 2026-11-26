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
        private readonly IAuditLogClient _auditLog;

        public InternalRefundsController(IRefundService service, IConfiguration config, IAuditLogClient auditLog)
        {
            _service = service;
            _config = config;
            _auditLog = auditLog;
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
                var result = await _service.RefundAsync(request, ct);
                if (result.Status == "Refunded")
                    _ = _auditLog.LogAsync(null, "system", "REFUND_CREATED", "PaymentRefund",
                        result.RefundId?.ToString(), null,
                        new { request.ReturnRequestId, request.OrderId, request.Amount, result.Status, result.Reference },
                        HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("manual")]
        public async Task<IActionResult> RecordManual([FromBody] RecordManualRefundDto request, CancellationToken ct)
        {
            var expected = _config["TicketApi:InternalApiKey"];
            var supplied = Request.Headers["X-Internal-Api-Key"].ToString();
            if (string.IsNullOrWhiteSpace(expected) || !string.Equals(supplied, expected, StringComparison.Ordinal))
                return Unauthorized(new { success = false, message = "Invalid service credentials." });

            try
            {
                var result = await _service.RecordManualRefundAsync(request, ct);
                if (result.Status == "Refunded")
                    _ = _auditLog.LogAsync(request.ProcessedByUserId, "staff", "MANUAL_BANK_REFUND_RECORDED", "PaymentRefund",
                        result.RefundId?.ToString(), null,
                        new { request.ReturnRequestId, request.OrderId, request.Amount, request.Reason, result.Status },
                        HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
