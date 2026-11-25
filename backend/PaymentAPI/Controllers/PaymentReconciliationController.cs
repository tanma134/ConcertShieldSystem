using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Route("api/admin/payment-reconciliation/exceptions")]
    [Authorize(Roles = "Admin")]
    public class PaymentReconciliationController : BaseApiController
    {
        private readonly IPaymentReconciliationService _service;

        public PaymentReconciliationController(
            IPaymentReconciliationService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetExceptions(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? orderId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _service.GetExceptionsAsync(
                    from,
                    to,
                    orderId,
                    page,
                    pageSize);

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost("{paymentTransactionId}/resolve")]
        public async Task<IActionResult> ResolveException(
            int paymentTransactionId,
            [FromBody] ResolvePaymentReconciliationRequestDto request)
        {
            if (!IsAdmin)
                return Forbid();

            try
            {
                var result = await _service.ResolveExceptionAsync(
                    paymentTransactionId,
                    request,
                    CurrentUserId);

                return Ok(new
                {
                    success = true,
                    data = result,
                    message = "Exception resolved successfully."
                });
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Lay danh sach hang cho cac giao dich thanh toan hoan tien that bai danh cho Admin
        [HttpGet("/api/admin/failed-transaction-queue")]
        public async Task<IActionResult> GetFailedTransactionQueue(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!IsAdmin)
                return Forbid();

            try
            {
                var result = await _service.GetFailedTransactionQueueAsync(
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
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("/api/admin/ticket-issuance-recovery-queue")]
        public async Task<IActionResult> GetTicketIssuanceRecoveryQueue(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!IsAdmin)
                return Forbid();

            try
            {
                var result =
                    await _service.GetTicketIssuanceRecoveryQueueAsync(
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
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }
    }
}
