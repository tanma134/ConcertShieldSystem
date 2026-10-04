using System.Transactions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.API;
using PaymentAPI.DTOs;
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

        public VnPayController(IVnPayService vnPayService, IPaymentTransactionService paymentTransactionService, ITicketApiClient ticketApiClient)
        {
            _vnPayService = vnPayService;
            _paymentTransactionService = paymentTransactionService;
            _ticketApiClient = ticketApiClient;
        }

        [HttpPost("create-payment")]
        public async Task<ActionResult<CreateVnPayPaymentResponseDto>> CreatePayment([FromBody] CreateVnPayPaymentRequestDto request)
        {
            if (request.OrderId <= 0 || request.FinalAmount <= 0)
                return BadRequest("Invalid order or amount.");

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
                checked((int)request.FinalAmount),
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

            if (result == "Payment successful")
            {
                if (!int.TryParse(orderIdText, out var orderId) ||
                    !long.TryParse(query["vnp_Amount"], out var vnpAmount) ||
                    vnpAmount % 100 != 0)
                {
                    frontendStatus = "pending";
                }
                else
                {
                    var confirmed = await _ticketApiClient.ConfirmOrderPaymentAsync(
                        orderId,
                        new ConfirmOrderPaymentRequestDTO
                        {
                            Amount = vnpAmount / 100,
                            TransactionRef = transactionNo
                        });

                    frontendStatus = confirmed ? "success" : "pending";
                }
            }
            else if (result == "Invalid signature")
            {
                frontendStatus = "invalid";
            }

            var frontendUrl =
                $"http://localhost:5173/payment/result" +
                $"?status={frontendStatus}" +
                (int.TryParse(orderIdText, out _)
                    ? $"&orderId={Uri.EscapeDataString(orderIdText)}"
                    : "");

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
