using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services
{
    public class RefundService : IRefundService
    {
        private readonly PaymentDbContext _db;
        private readonly IPaymentTransactionRepository _payments;
        private readonly IHttpClientFactory _http;
        private readonly VnPayConfig _vnPay;
        private readonly IConfiguration _config;
        private readonly ILogger<RefundService> _logger;

        public RefundService(
            PaymentDbContext db,
            IPaymentTransactionRepository payments,
            IHttpClientFactory http,
            IOptions<VnPayConfig> vnPay,
            IConfiguration config,
            ILogger<RefundService> logger)
        {
            _db = db;
            _payments = payments;
            _http = http;
            _vnPay = vnPay.Value;
            _config = config;
            _logger = logger;
        }

        // Internal refund for ticket return requests (called by TicketAPI)
        public async Task<RefundResultDto> RefundAsync(CreateRefundRequestDto request, CancellationToken ct = default)
        {
            if (request.ReturnRequestId <= 0 || request.OrderId <= 0 || request.Amount < 0)
                throw new ArgumentException("Invalid refund request.");

            var refund = await _db.PaymentRefunds.FirstOrDefaultAsync(r => r.ReturnRequestId == request.ReturnRequestId, ct);
            if (refund?.Status == "Refunded")
                return new RefundResultDto { RefundId = refund.PaymentRefundId, Status = "Refunded", Reference = refund.GatewayRefundRef };

            if (refund == null)
            {
                refund = new PaymentRefund
                {
                    ReturnRequestId = request.ReturnRequestId,
                    OrderId = request.OrderId,
                    Amount = request.Amount,
                    Gateway = "VNPay",
                    Status = "Pending",
                    RefundType = "Full",
                    Reason = "Ticket Return Request",
                    CreatedAt = DateTime.UtcNow
                };
                _db.PaymentRefunds.Add(refund);
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    _db.Entry(refund).State = EntityState.Detached;
                    refund = await _db.PaymentRefunds.FirstAsync(r => r.ReturnRequestId == request.ReturnRequestId, ct);
                    if (refund.Status == "Refunded")
                        return new RefundResultDto { RefundId = refund.PaymentRefundId, Status = "Refunded", Reference = refund.GatewayRefundRef };
                }
            }

            return await ExecuteRefundAsync(refund, request.OrderId, request.Amount, ct);
        }

        // UC 3.16.20: Process Full / Partial Refund
        public async Task<RefundResultDto> ProcessRefundAsync(ProcessRefundRequestDto request, int actorId, string actorName, CancellationToken ct = default)
        {
            if (request.OrderId <= 0)
                throw new ArgumentException("Valid order ID is required.");
            if (request.Amount <= 0)
                throw new ArgumentException("Refund amount must be greater than zero.");

            var payment = await _payments.GetLatestByOrderIdAsync(request.OrderId, "VNPay");
            if (payment == null || payment.Status != "Success")
                throw new InvalidOperationException("No successful payment transaction found for this order.");

            var alreadyRefunded = await _db.PaymentRefunds
                .Where(r => r.OrderId == request.OrderId && r.Status == "Refunded")
                .SumAsync(r => (long?)r.Amount, ct) ?? 0;

            var maxRefundable = payment.Amount - alreadyRefunded;
            if (request.Amount > maxRefundable)
            {
                // EX-01: Refund Amount Exceeds Limit
                throw new ArgumentException($"Refund amount exceeds the maximum refundable amount (₫{maxRefundable:N0}).");
            }

            var customerName = ExtractCustomerName(payment.WebhookPayload) ?? $"Customer (Order #{request.OrderId})";

            var refund = new PaymentRefund
            {
                OrderId = request.OrderId,
                Amount = request.Amount,
                Gateway = "VNPay",
                Status = "Pending",
                RefundType = string.Equals(request.RefundType, "Partial", StringComparison.OrdinalIgnoreCase) ? "Partial" : "Full",
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Staff Initiated Refund" : request.Reason.Trim(),
                ProcessedByUserId = actorId,
                ProcessedByUserName = string.IsNullOrWhiteSpace(actorName) ? "Staff" : actorName.Trim(),
                CustomerName = customerName,
                CreatedAt = DateTime.UtcNow
            };

            _db.PaymentRefunds.Add(refund);
            await _db.SaveChangesAsync(ct);

            return await ExecuteRefundAsync(refund, request.OrderId, request.Amount, ct);
        }

        // UC 3.16.21: Retry Failed Refund
        public async Task<RefundResultDto> RetryRefundAsync(int refundId, string strategy, int actorId, string actorName, CancellationToken ct = default)
        {
            var refund = await _db.PaymentRefunds.FirstOrDefaultAsync(r => r.PaymentRefundId == refundId, ct)
                ?? throw new KeyNotFoundException($"Refund record #{refundId} not found.");

            // A.1: Previous Refund Already Confirmed
            if (refund.Status == "Refunded")
            {
                return new RefundResultDto
                {
                    RefundId = refund.PaymentRefundId,
                    Status = "Refunded",
                    Reference = refund.GatewayRefundRef
                };
            }

            refund.RetryCount++;
            refund.LastRetryAt = DateTime.UtcNow;
            refund.ProcessedByUserId = actorId;
            refund.ProcessedByUserName = string.IsNullOrWhiteSpace(actorName) ? refund.ProcessedByUserName : actorName.Trim();
            refund.Status = "Pending";
            await _db.SaveChangesAsync(ct);

            return await ExecuteRefundAsync(refund, refund.OrderId, refund.Amount, ct);
        }

        private async Task<RefundResultDto> ExecuteRefundAsync(PaymentRefund refund, int orderId, long amount, CancellationToken ct)
        {
            try
            {
                var payment = await _payments.GetLatestByOrderIdAsync(orderId, "VNPay");
                if (payment == null || payment.Status != "Success")
                    return await FailAsync(refund, "No successful payment was found for this order.", "ORDER_NOT_PAID", ct);

                if (amount == 0)
                    return await SucceedAsync(refund, "NO-MONEY-" + refund.PaymentRefundId, 0, ct);

                var mode = _config["Refund:Mode"] ?? "Ledger";
                if (!string.Equals(mode, "VnPay", StringComparison.OrdinalIgnoreCase))
                {
                    var ledgerRef = $"LEDGER-RFD-{refund.PaymentRefundId:D4}";
                    return await SucceedAsync(refund, ledgerRef, amount, ct);
                }

                // Call VNPay Refund API
                var (ok, reference, error, errCode) = await CallVnPayRefundAsync(payment, amount, ct);
                if (ok)
                {
                    return await SucceedAsync(refund, reference!, amount, ct);
                }
                else
                {
                    // EX-02: VNPay Refund Failure/Timeout
                    return await FailAsync(refund, error ?? "VNPay refund request failed or timed out.", errCode ?? "E5003", ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Refund execution error for refund #{Id}", refund.PaymentRefundId);
                return await FailAsync(refund, "Refund could not be completed. Please retry later.", "INTERNAL_ERROR", ct);
            }
        }

        private async Task<RefundResultDto> SucceedAsync(PaymentRefund refund, string reference, long gatewayAmount, CancellationToken ct)
        {
            refund.Status = "Refunded";
            refund.GatewayRefundRef = reference.Length > 100 ? reference[..100] : reference;
            refund.GatewayAmount = gatewayAmount;
            refund.ReconciliationStatus = "Matched";
            refund.ErrorMessage = null;
            refund.GatewayErrorCode = null;
            refund.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new RefundResultDto
            {
                RefundId = refund.PaymentRefundId,
                Status = "Refunded",
                Reference = refund.GatewayRefundRef
            };
        }

        private async Task<RefundResultDto> FailAsync(PaymentRefund refund, string error, string errCode, CancellationToken ct)
        {
            refund.Status = "Failed";
            refund.ErrorMessage = error.Length > 500 ? error[..500] : error;
            refund.GatewayErrorCode = errCode.Length > 50 ? errCode[..50] : errCode;
            refund.ReconciliationStatus = "Mismatched";
            await _db.SaveChangesAsync(ct);

            return new RefundResultDto
            {
                RefundId = refund.PaymentRefundId,
                Status = "Failed",
                Error = refund.ErrorMessage,
                ErrorCode = refund.GatewayErrorCode
            };
        }

        // Order refund summary and balance inspection
        public async Task<OrderRefundSummaryDto> GetOrderRefundSummaryAsync(int orderId, CancellationToken ct = default)
        {
            var payment = await _payments.GetLatestByOrderIdAsync(orderId, "VNPay");
            var originalAmount = payment?.Amount ?? 0;

            var refunds = await _db.PaymentRefunds
                .Where(r => r.OrderId == orderId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(ct);

            var alreadyRefunded = refunds
                .Where(r => r.Status == "Refunded")
                .Sum(r => r.Amount);

            var customerName = ExtractCustomerName(payment?.WebhookPayload);

            return new OrderRefundSummaryDto
            {
                OrderId = orderId,
                CustomerName = customerName ?? $"Customer #{orderId}",
                TransactionId = payment?.GatewayTransactionRef != null ? $"#PAY-{payment.GatewayTransactionRef}" : $"#PAY-{orderId}",
                PurchaseDate = payment?.RequestedAt,
                OriginalAmount = originalAmount,
                AlreadyRefunded = alreadyRefunded,
                MaxRefundableAmount = Math.Max(0, originalAmount - alreadyRefunded),
                OrderStatus = payment?.Status == "Success" ? "Paid" : payment?.Status ?? "Unknown",
                PreviousRefunds = refunds.Select(MapRefundItem).ToList()
            };
        }

        // UC 3.16.22: Refund Reconciliation Report
        public async Task<RefundReconciliationReportDto> GetReconciliationReportAsync(ReconciliationFilterDto filter, CancellationToken ct = default)
        {
            var query = _db.PaymentRefunds.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                var cleanDigits = new string(term.Where(char.IsDigit).ToArray());
                if (int.TryParse(cleanDigits, out var numericId))
                {
                    query = query.Where(r => r.PaymentRefundId == numericId || r.OrderId == numericId);
                }
                else
                {
                    query = query.Where(r =>
                        (r.CustomerName != null && r.CustomerName.ToLower().Contains(term)) ||
                        (r.GatewayRefundRef != null && r.GatewayRefundRef.ToLower().Contains(term)));
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(filter.Status, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || filter.Status.Equals("Refunded", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(r => r.Status == "Refunded");
                else
                    query = query.Where(r => r.Status == filter.Status);
            }

            if (!string.IsNullOrWhiteSpace(filter.MatchStatus) && !string.Equals(filter.MatchStatus, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.ReconciliationStatus == filter.MatchStatus);
            }

            if (filter.EventId.HasValue && filter.EventId.Value > 0)
            {
                query = query.Where(r => r.EventId == filter.EventId.Value);
            }

            var availableEvents = await _db.PaymentRefunds.AsNoTracking()
                .Where(r => r.EventId != null)
                .GroupBy(r => new { r.EventId, r.EventName })
                .Select(g => new EventOptionDto
                {
                    EventId = g.Key.EventId!.Value,
                    EventName = string.IsNullOrWhiteSpace(g.Key.EventName) ? $"Event #{g.Key.EventId}" : g.Key.EventName,
                    RefundCount = g.Count()
                })
                .OrderByDescending(e => e.RefundCount)
                .ToListAsync(ct);

            var allRefunds = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

            // Calculate KPI metrics
            var totalRefunds = allRefunds.Count;
            var totalAmount = allRefunds.Where(r => r.Status == "Refunded").Sum(r => r.Amount);
            var pendingCount = allRefunds.Count(r => r.Status == "Pending");
            var mismatchedCount = allRefunds.Count(r => r.ReconciliationStatus == "Mismatched" || r.Status == "Failed");

            return new RefundReconciliationReportDto
            {
                TotalRefunds = totalRefunds,
                TotalAmount = totalAmount,
                PendingReconciliation = pendingCount,
                MismatchedCount = mismatchedCount,
                Refunds = allRefunds.Select(MapRefundItem).ToList(),
                AvailableEvents = availableEvents
            };
        }

        public async Task<RefundItemDto?> GetRefundByIdAsync(int refundId, CancellationToken ct = default)
        {
            var refund = await _db.PaymentRefunds.AsNoTracking().FirstOrDefaultAsync(r => r.PaymentRefundId == refundId, ct);
            return refund == null ? null : MapRefundItem(refund);
        }

        private static RefundItemDto MapRefundItem(PaymentRefund r)
        {
            var displayStatus = r.Status switch
            {
                "Refunded" => "Completed",
                "Pending" => "Pending",
                "Failed" => "Failed",
                _ => r.Status
            };

            var matchStatus = r.Status switch
            {
                "Refunded" => "Completed",
                "Failed" => "Mismatched",
                "Pending" => "Pending",
                _ => r.ReconciliationStatus
            };

            return new RefundItemDto
            {
                PaymentRefundId = r.PaymentRefundId,
                RefundReference = $"#RFD-{r.PaymentRefundId:D4}",
                OrderId = r.OrderId,
                CustomerName = string.IsNullOrWhiteSpace(r.CustomerName) ? $"Customer (Order #{r.OrderId})" : r.CustomerName,
                EventId = r.EventId,
                EventName = r.EventName,
                RefundType = r.RefundType,
                Amount = r.Amount,
                GatewayAmount = r.GatewayAmount ?? (r.Status == "Refunded" ? r.Amount : 0),
                Status = displayStatus,
                MatchStatus = matchStatus,
                Reason = r.Reason,
                ProcessedByUserName = r.ProcessedByUserName,
                GatewayErrorCode = r.GatewayErrorCode,
                ErrorMessage = r.ErrorMessage,
                RetryCount = r.RetryCount,
                CreatedAt = r.CreatedAt,
                CompletedAt = r.CompletedAt
            };
        }

        private async Task<(bool Ok, string? Reference, string? Error, string? Code)> CallVnPayRefundAsync(
            PaymentTransaction payment, long amount, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(payment.GatewayTransactionRef))
                return (false, null, "The original VNPay transaction number is missing.", "E0001");

            var payDate = ReadPayDate(payment.WebhookPayload);
            if (payDate == null)
                return (false, null, "The original VNPay payment date is missing.", "E0002");

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

            try
            {
                using var response = await client.PostAsJsonAsync(url, body, ct);
                var text = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                    return (false, null, $"VNPay returned HTTP {(int)response.StatusCode}.", "E5003");

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var code = root.TryGetProperty("vnp_ResponseCode", out var c) ? c.GetString() : null;
                if (code == "00")
                {
                    var no = root.TryGetProperty("vnp_TransactionNo", out var t) ? t.GetString() : null;
                    return (true, "VNPAY-" + (string.IsNullOrWhiteSpace(no) ? requestId[..12] : no), null, null);
                }

                var message = root.TryGetProperty("vnp_Message", out var m) ? m.GetString() : null;
                return (false, null, $"VNPay rejected the refund (code {code ?? "?"}) {message}".Trim(), code ?? "E9999");
            }
            catch (Exception ex)
            {
                return (false, null, $"Transaction timeout - bank gateway unavailable. {ex.Message}", "E5003");
            }
        }

        private static string? ExtractCustomerName(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("vnp_OrderInfo", out var o))
                {
                    return o.GetString();
                }
            }
            catch { }
            return null;
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
