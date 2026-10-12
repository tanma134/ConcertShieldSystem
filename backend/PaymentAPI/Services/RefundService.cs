using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services
{
    // Pays money back for an approved ticket return.
    // Refund:Mode = "Ledger" (default): records the refund in payment_refunds; finance pays it out
    //                                   (right for demos and the VNPay sandbox).
    // Refund:Mode = "VnPay":            also calls the VNPay refund API for the original transaction.
    public class RefundService : IRefundService
    {
        private readonly PaymentDbContext _db;
        private readonly IPaymentTransactionRepository _payments;
        private readonly IHttpClientFactory _http;
        private readonly VnPayConfig _vnPay;
        private readonly IConfiguration _config;
        private readonly ILogger<RefundService> _logger;

        public RefundService(PaymentDbContext db, IPaymentTransactionRepository payments, IHttpClientFactory http,
            IOptions<VnPayConfig> vnPay, IConfiguration config, ILogger<RefundService> logger)
        {
            _db = db;
            _payments = payments;
            _http = http;
            _vnPay = vnPay.Value;
            _config = config;
            _logger = logger;
        }

        public async Task<RefundResultDto> RefundAsync(CreateRefundRequestDto request, CancellationToken ct = default)
        {
            if (request.ReturnRequestId <= 0 || request.OrderId <= 0 || request.Amount < 0)
                throw new ArgumentException("Invalid refund request.");

            var refund = await _db.PaymentRefunds.FirstOrDefaultAsync(r => r.ReturnRequestId == request.ReturnRequestId, ct);
            if (refund?.Status == "Refunded")
                return new RefundResultDto { Status = "Refunded", Reference = refund.GatewayRefundRef };

            if (refund == null)
            {
                refund = new PaymentRefund
                {
                    ReturnRequestId = request.ReturnRequestId,
                    OrderId = request.OrderId,
                    Amount = request.Amount,
                    Gateway = "VNPay",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                _db.PaymentRefunds.Add(refund);
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // A parallel call created the same row; use it.
                    _db.Entry(refund).State = EntityState.Detached;
                    refund = await _db.PaymentRefunds.FirstAsync(r => r.ReturnRequestId == request.ReturnRequestId, ct);
                    if (refund.Status == "Refunded")
                        return new RefundResultDto { Status = "Refunded", Reference = refund.GatewayRefundRef };
                }
            }

            try
            {
                var payment = await _payments.GetLatestByOrderIdAsync(request.OrderId, "VNPay");
                if (payment == null || payment.Status != "Success")
                    return await FailAsync(refund, "No successful payment was found for this order.", ct);

                if (request.Amount > payment.Amount)
                    return await FailAsync(refund, "Refund amount is greater than the amount paid.", ct);

                var alreadyRefunded = await _db.PaymentRefunds
                    .Where(r => r.OrderId == request.OrderId && r.Status == "Refunded" && r.ReturnRequestId != request.ReturnRequestId)
                    .SumAsync(r => (long?)r.Amount, ct) ?? 0;
                if (alreadyRefunded + request.Amount > payment.Amount)
                    return await FailAsync(refund, "Total refunds would exceed the amount paid for this order.", ct);

                refund.Amount = request.Amount;

                // A free ticket has nothing to pay back.
                if (request.Amount == 0)
                    return await SucceedAsync(refund, "NO-MONEY-" + refund.PaymentRefundId, ct);

                var mode = _config["Refund:Mode"] ?? "Ledger";
                if (!string.Equals(mode, "VnPay", StringComparison.OrdinalIgnoreCase))
                    return await SucceedAsync(refund, "LEDGER-" + refund.PaymentRefundId, ct);

                var (ok, reference, error) = await CallVnPayRefundAsync(payment, request.Amount, ct);
                return ok ? await SucceedAsync(refund, reference!, ct) : await FailAsync(refund, error ?? "VNPay refund failed.", ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Refund for return request {ReturnRequestId} failed", request.ReturnRequestId);
                return await FailAsync(refund, "Refund could not be completed. Please retry later.", ct);
            }
        }

        private async Task<RefundResultDto> SucceedAsync(PaymentRefund refund, string reference, CancellationToken ct)
        {
            refund.Status = "Refunded";
            refund.GatewayRefundRef = reference.Length > 100 ? reference[..100] : reference;
            refund.ErrorMessage = null;
            refund.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new RefundResultDto { Status = "Refunded", Reference = refund.GatewayRefundRef };
        }

        private async Task<RefundResultDto> FailAsync(PaymentRefund refund, string error, CancellationToken ct)
        {
            refund.Status = "Failed";
            refund.ErrorMessage = error.Length > 500 ? error[..500] : error;
            await _db.SaveChangesAsync(ct);
            return new RefundResultDto { Status = "Failed", Error = refund.ErrorMessage };
        }

        // VNPay "refund" command (API 2.1.0). Not exercised against the live sandbox in this repository:
        // check the field values against your merchant's sandbox before switching Refund:Mode to VnPay.
        private async Task<(bool Ok, string? Reference, string? Error)> CallVnPayRefundAsync(
            PaymentTransaction payment, long amount, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(payment.GatewayTransactionRef))
                return (false, null, "The original VNPay transaction number is missing.");

            var payDate = ReadPayDate(payment.WebhookPayload);
            if (payDate == null)
                return (false, null, "The original VNPay payment date is missing.");

            var requestId = Guid.NewGuid().ToString("N");
            var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
            var transactionType = amount == payment.Amount ? "02" : "03";
            var txnRef = payment.OrderId.ToString();
            var vnpAmount = (amount * 100).ToString();
            const string createBy = "ConcertShield";
            const string ip = "127.0.0.1";
            var orderInfo = $"Hoan tien don hang {payment.OrderId}";

            var hashData = string.Join("|", requestId, "2.1.0", "refund", _vnPay.TmnCode, transactionType, txnRef,
                vnpAmount, payment.GatewayTransactionRef, payDate, createBy, createDate, ip, orderInfo);

            var body = new Dictionary<string, string>
            {
                ["vnp_RequestId"] = requestId,
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "refund",
                ["vnp_TmnCode"] = _vnPay.TmnCode,
                ["vnp_TransactionType"] = transactionType,
                ["vnp_TxnRef"] = txnRef,
                ["vnp_Amount"] = vnpAmount,
                ["vnp_OrderInfo"] = orderInfo,
                ["vnp_TransactionNo"] = payment.GatewayTransactionRef,
                ["vnp_TransactionDate"] = payDate,
                ["vnp_CreateBy"] = createBy,
                ["vnp_CreateDate"] = createDate,
                ["vnp_IpAddr"] = ip,
                ["vnp_SecureHash"] = HmacSha512(_vnPay.HashSecret, hashData)
            };

            var url = _config["Refund:VnPayApiUrl"] ?? "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var response = await client.PostAsJsonAsync(url, body, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return (false, null, $"VNPay returned HTTP {(int)response.StatusCode}.");

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var code = root.TryGetProperty("vnp_ResponseCode", out var c) ? c.GetString() : null;
            if (code == "00")
            {
                var no = root.TryGetProperty("vnp_TransactionNo", out var t) ? t.GetString() : null;
                return (true, "VNPAY-" + (string.IsNullOrWhiteSpace(no) ? requestId[..12] : no), null);
            }

            var message = root.TryGetProperty("vnp_Message", out var m) ? m.GetString() : null;
            return (false, null, $"VNPay rejected the refund (code {code ?? "?"}) {message}".Trim());
        }

        private static string? ReadPayDate(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                return doc.RootElement.TryGetProperty("vnp_PayDate", out var d) ? d.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string HmacSha512(string key, string data)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        }
    }
}
