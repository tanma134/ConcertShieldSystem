using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaymentAPI.API;
using PaymentAPI.DTOs;
using PaymentAPI.Repositories;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VnPayController : ControllerBase
    {
        private readonly IVnPayService _vnPayService;
        private readonly IPaymentTransactionService _paymentTransactionService;
        private readonly ITicketApiClient _ticketApiClient;
        private readonly IPaymentTransactionRepository _transactions;
        private readonly ILogger<VnPayController> _logger;
        private readonly IAuditLogClient _auditLog;

        public VnPayController(IVnPayService vnPayService, IPaymentTransactionService paymentTransactionService, ITicketApiClient ticketApiClient,
            IPaymentTransactionRepository transactions, ILogger<VnPayController> logger, IAuditLogClient auditLog)
        {
            _vnPayService = vnPayService;
            _paymentTransactionService = paymentTransactionService;
            _ticketApiClient = ticketApiClient;
            _transactions = transactions;
            _logger = logger;
            _auditLog = auditLog;
        }

        [Authorize(Roles = "Customer")]
        [EnableRateLimiting(PaymentRateLimitPolicies.Payment)]
        [HttpPost("reconcile/{orderId:int}")]
        public async Task<IActionResult> Reconcile(int orderId)
        {
            var transaction = await _transactions.GetLatestByOrderIdAsync(orderId, "VNPay");
            if (transaction == null || transaction.Status != "Success" || string.IsNullOrWhiteSpace(transaction.GatewayTransactionRef))
            {
                return Ok(new
                {
                    success = false,
                    message = "VNPay has not reported a successful payment for this order yet."
                });
            }

            var (ok, error) = await _ticketApiClient.ConfirmOrderPaymentWithReasonAsync(
                orderId,
                new ConfirmOrderPaymentRequestDTO
                {
                    Amount = transaction.Amount,
                    TransactionRef = transaction.GatewayTransactionRef
                });

            if (ok)
            {
                var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                _ = _auditLog.LogAsync(int.TryParse(raw, out var uid) ? uid : null, "user", "PAYMENT_RECONCILED", "Order",
                    orderId.ToString(), null,
                    new { OrderId = orderId, transaction.Amount, TransactionRef = transaction.GatewayTransactionRef },
                    HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier);
            }

            return Ok(new
            {
                success = ok,
                message = ok ? "Order payment confirmed." : error
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting(PaymentRateLimitPolicies.Payment)]
        [HttpPost("create-payment")]
        public async Task<ActionResult<CreateVnPayPaymentResponseDto>> CreatePayment([FromBody] CreateVnPayPaymentRequestDto request)
        {
            if (request.OrderId <= 0 || request.FinalAmount <= 0)
                return BadRequest("Invalid order or amount.");

            if (request.FinalAmount > 999_999_999L)
                return BadRequest("Order amount exceeds the VNPay per-transaction limit (999,999,999 VND).");

            var clientIp = string.IsNullOrWhiteSpace(request.ClientIp)
                ? HttpContext.Connection.RemoteIpAddress?.ToString()
                : request.ClientIp;

            if (string.IsNullOrWhiteSpace(clientIp) ||
                clientIp == "::1" ||
                clientIp.Contains(':'))
            {
                clientIp = "127.0.0.1";
            }

            var pendingTransaction = await _transactions.GetPendingByOrderIdAsync(request.OrderId, "VNPay");

            if (pendingTransaction == null)
            {
                await _paymentTransactionService.CreatePaymentTransactionAsync(
                    request.OrderId,
                    request.FinalAmount,
                    request.UserId,
                    clientIp,
                    request.DeviceFp,
                    request.SessionId);
            }
            else if (pendingTransaction.Amount != request.FinalAmount)
            {
                return Conflict("A pending payment exists with a different amount.");
            }

            var paymentUrl = _vnPayService.CreatePaymentUrl(
                request.OrderId,
                request.FinalAmount,
                clientIp);

            return Ok(new CreateVnPayPaymentResponseDto
            {
                PaymentUrl = paymentUrl
            });
        }

        [AllowAnonymous]
        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnPayReturn()
        {
            var query = Request.Query;
            var rawQuery = Request.QueryString.Value ?? "";

            var result = await _vnPayService.HandleVNPayReturn(query, rawQuery);

            var orderIdText = query["vnp_TxnRef"].ToString();
            var transactionNo = query["vnp_TransactionNo"].ToString();

            var frontendStatus = "failed";
            string? reason = null;
            var vnpCode = query["vnp_ResponseCode"].ToString();

            if (result == "Payment successful")
            {
                if (!int.TryParse(orderIdText, out var orderId) ||
                    !long.TryParse(query["vnp_Amount"], out var vnpAmount) ||
                    vnpAmount % 100 != 0)
                {
                    frontendStatus = "pending";
                    reason = "The payment amount returned by VNPay is invalid.";
                }
                else
                {
                    var (confirmed, error) = await _ticketApiClient.ConfirmOrderPaymentWithReasonAsync(
                        orderId,
                        new ConfirmOrderPaymentRequestDTO
                        {
                            Amount = vnpAmount / 100,
                            TransactionRef = transactionNo
                        });

                    frontendStatus = confirmed ? "success" : "pending";
                    if (!confirmed) reason = error;
                }
            }
            else if (result == "Invalid signature")
            {
                frontendStatus = "invalid";
            }
            else if (result == "Payment transaction not found or amount does not match")
            {
                frontendStatus = "failed";
                reason = "No matching payment was found for this order, or the amount does not match.";
            }
            else if (result == "Payment cancelled")
            {
                if (!int.TryParse(orderIdText, out var orderId))
                {
                    frontendStatus = "failed";
                    reason = "The order ID returned by VNPay is invalid.";
                }
                else if (await _ticketApiClient.CancelPendingOrderAsync(orderId))
                {
                    frontendStatus = "cancelled";
                }
                else
                {
                    frontendStatus = "pending";
                    reason = "Payment was cancelled, but the order could not be cancelled yet.";
                }
            }

            var frontendUrl =
                $"http://localhost:5173/payment/result" +
                $"?status={frontendStatus}" +
                (int.TryParse(orderIdText, out _)
                    ? $"&orderId={Uri.EscapeDataString(orderIdText)}"
                    : "") +
                (string.IsNullOrWhiteSpace(vnpCode) ? "" : $"&code={Uri.EscapeDataString(vnpCode)}") +
                (string.IsNullOrWhiteSpace(reason)
                    ? ""
                    : $"&reason={Uri.EscapeDataString(reason.Length > 300 ? reason[..300] : reason)}");

            return Redirect(frontendUrl);
        }

        [AllowAnonymous]
        [HttpGet("vnpay-ipn")]
        public async Task<IActionResult> VnPayIpn()
        {
            var query = Request.Query;
            var rawQuery = Request.QueryString.Value ?? "";

            try
            {
                var result = await _vnPayService.HandleVNPayReturn(query, rawQuery);

                if (result == "Invalid signature")
                {
                    return Ok(new { RspCode = "97", Message = "Invalid signature" });
                }

                if (result == "Payment transaction not found or amount does not match")
                {
                    return Ok(new { RspCode = "04", Message = "Invalid transaction" });
                }

                if (result == "Payment successful")
                {
                    var orderIdText = query["vnp_TxnRef"].ToString();

                    if (!int.TryParse(orderIdText, out var orderId) ||
                        !long.TryParse(query["vnp_Amount"], out var vnpAmount) ||
                        vnpAmount <= 0 ||
                        vnpAmount % 100 != 0)
                    {
                        return Ok(new { RspCode = "04", Message = "Invalid amount or order" });
                    }

                    var transactionNo = query["vnp_TransactionNo"].ToString();

                    var confirmed = await _ticketApiClient.ConfirmOrderPaymentAsync(
                        orderId,
                        new ConfirmOrderPaymentRequestDTO
                        {
                            Amount = vnpAmount / 100,
                            TransactionRef = transactionNo
                        });

                    if (!confirmed)
                    {
                        return Ok(new { RspCode = "99", Message = "Order confirmation failed" });
                    }
                }
                else if (result == "Payment cancelled")
                {
                    var orderIdText = query["vnp_TxnRef"].ToString();

                    if (!int.TryParse(orderIdText, out var orderId))
                    {
                        return Ok(new
                        {
                            RspCode = "04",
                            Message = "Invalid order ID"
                        });
                    }

                    var cancelled =
                        await _ticketApiClient.CancelPendingOrderAsync(orderId);

                    if (!cancelled)
                    {
                        return Ok(new
                        {
                            RspCode = "99",
                            Message = "Could not cancel the pending order"
                        });
                    }
                }

                return Ok(new { RspCode = "00", Message = "Success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VNPay IPN processing failed");
                return Ok(new { RspCode = "99", Message = "Processing error" });
            }
        }
    }
}