using System.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.API;
using PaymentAPI.DTOs;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using TicketAPI.DTOs;

namespace PaymentAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VnPayController : ControllerBase
    {
        private readonly IVnPayService _vnPayService;
        private readonly IPaymentTransactionService _paymentTransactionService;
        private readonly ITicketApiClient _ticketApiClient;
        private readonly IPaymentTransactionRepository _transactions;

        public VnPayController(IVnPayService vnPayService, IPaymentTransactionService paymentTransactionService, ITicketApiClient ticketApiClient,
            IPaymentTransactionRepository transactions)
        {
            _vnPayService = vnPayService;
            _paymentTransactionService = paymentTransactionService;
            _ticketApiClient = ticketApiClient;
            _transactions = transactions;
        }

        // The "Check again" button on the payment result page.
        // VNPay already confirmed this payment (signature checked when the customer came back), so the
        // stored Success transaction is replayed to TicketAPI. Safe to repeat: TicketAPI treats a Paid order as done.
        [Authorize]
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

            return Ok(new
            {
                success = ok,
                message = ok ? "Order payment confirmed." : error
            });
        }

        [HttpPost("create-payment")]
        public async Task<ActionResult<CreateVnPayPaymentResponseDto>> CreatePayment([FromBody] CreateVnPayPaymentRequestDto request)
        {
            if (request.OrderId <= 0 || request.FinalAmount <= 0)
                return BadRequest("Invalid order or amount.");

            // VNPay giới hạn dưới 1 tỷ VND / giao dịch.
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

            await _paymentTransactionService.CreatePaymentTransactionAsync(
                request.OrderId,
                request.FinalAmount);

            var paymentUrl = _vnPayService.CreatePaymentUrl(
                request.OrderId,
                request.FinalAmount,
                clientIp);

            return Ok(new CreateVnPayPaymentResponseDto
            {
                PaymentUrl = paymentUrl
            });
        }

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

                return Ok(new { RspCode = "00", Message = "Success" });
            }
            catch (Exception ex)
            {
                return Ok(new { RspCode = "99", Message = "Processing error" });
            }
        }
    }
}
