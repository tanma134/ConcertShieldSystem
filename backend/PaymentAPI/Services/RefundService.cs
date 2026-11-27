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
        private readonly IPaymentRefundRepository _refunds;
        private readonly IPaymentTransactionRepository _payments;
        private readonly IHttpClientFactory _http;
        private readonly VnPayConfig _vnPay;
        private readonly IConfiguration _config;
        private readonly ILogger<RefundService> _logger;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public RefundService(
            IPaymentRefundRepository refunds,
            IPaymentTransactionRepository payments,
            IHttpClientFactory http,
            IOptions<VnPayConfig> vnPay,
            IConfiguration config,
            ILogger<RefundService> logger)
        {
            _refunds = refunds;
            _payments = payments;
            _http = http;
            _vnPay = vnPay.Value;
            _config = config;
            _logger = logger;
        }

        public RefundService(
            PaymentDbContext db,
            IPaymentTransactionRepository payments,
            IHttpClientFactory http,
            IOptions<VnPayConfig> vnPay,
            IConfiguration config,
            ILogger<RefundService> logger)
            : this(new PaymentRefundRepository(db), payments, http, vnPay, config, logger)
        {
        }

        // Internal refund for ticket return requests (called by TicketAPI)
        public async Task<RefundResultDto> RefundAsync(CreateRefundRequestDto request, CancellationToken ct = default)
        {
            if (request.ReturnRequestId <= 0 || request.OrderId <= 0 || request.Amount < 0)
                throw new ArgumentException("Invalid refund request.");

            var refund = await _refunds.GetByReturnRequestIdAsync(request.ReturnRequestId, ct);
            if (refund?.Status == "Refunded")
            {
                if (refund.OrderId != request.OrderId || refund.Amount != request.Amount)
                    throw new InvalidOperationException("This return already has a different refund record.");
                return new RefundResultDto { RefundId = refund.PaymentRefundId, Status = "Refunded", Reference = refund.GatewayRefundRef };
            }

            if (refund == null)
            {
                refund = new PaymentRefund
                {
                    ReturnRequestId = request.ReturnRequestId,
                    OrderId = request.OrderId,
                    Amount = request.Amount,
                    CustomerName = request.CustomerName,
                    EventId = request.EventId,
                    EventName = request.EventName,
                    Gateway = "VNPay",
                    Status = "Pending",
                    RefundType = "Full",
                    Reason = "Ticket Return Request",
                    CreatedAt = DateTime.UtcNow
                };
                await _refunds.AddAsync(refund, ct);
                try
                {
                    await _refunds.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    _refunds.Detach(refund);
                    refund = await _refunds.GetByReturnRequestIdAsync(request.ReturnRequestId, ct) ?? throw new DbUpdateException("Refund was created concurrently.");
                    if (refund.Status == "Refunded")
                        return new RefundResultDto { RefundId = refund.PaymentRefundId, Status = "Refunded", Reference = refund.GatewayRefundRef };
                }
            }

            return await ExecuteRefundAsync(refund, request.OrderId, request.Amount, ct);
        }

        // Records a manual bank-transfer refund confirmed in TicketAPI so reconciliation reports stay in sync.
        public async Task<RefundResultDto> RecordManualRefundAsync(RecordManualRefundDto request, CancellationToken ct = default)
        {
            if (request.ReturnRequestId <= 0 || request.OrderId <= 0 || request.Amount < 0 || string.IsNullOrWhiteSpace(request.Reference))
                throw new ArgumentException("Invalid manual refund record.");

            var now = DateTime.UtcNow;
            var suppliedReference = request.Reference.Trim();
            var isSimulation = request.IsSimulation;
            if (isSimulation != suppliedReference.StartsWith("SIM-", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Simulation records require a SIM- reference and cannot be recorded as real transfers.");
            if (isSimulation && !_config.GetValue("Refund:AllowSimulation", false))
                throw new InvalidOperationException("Refund simulation is disabled in this environment.");
            var reference = isSimulation
                ? $"SIM-RFD-{request.ReturnRequestId:D6}"
                : suppliedReference;
            var gateway = isSimulation ? "SimulatedBankTransfer" : "ManualBankTransfer";
            var refund = await _refunds.GetByReturnRequestIdAsync(request.ReturnRequestId, ct);
            if (refund != null && refund.Status == "Refunded")
            {
                if (refund.OrderId != request.OrderId || refund.Amount != request.Amount || refund.GatewayRefundRef != reference || refund.Gateway != gateway)
                    throw new InvalidOperationException("This return already has a different refund record.");
                return new RefundResultDto { RefundId = refund.PaymentRefundId, Status = "Refunded", Reference = refund.GatewayRefundRef };
            }

            if (refund == null)
            {
                // The manual bank-transfer path can otherwise record more money back than
                // the order ever paid, because each return request is recorded on its own.
                // Enforce the same remaining-balance cap the gateway path already applies.
                // A simulation moves no money, so it is not capped against a real payment.
                if (!isSimulation && request.Amount > 0)
                {
                    var paid = await _payments.GetLatestByOrderIdAsync(request.OrderId, "VNPay");
                    if (paid == null || paid.Status != "Success")
                        throw new InvalidOperationException("No successful payment transaction found for this order.");

                    var alreadyRefunded = await _refunds.GetRefundedAmountByOrderIdAsync(request.OrderId, ct);
                    if (request.Amount > paid.Amount - alreadyRefunded)
                        throw new InvalidOperationException(
                            $"Refund amount exceeds the remaining refundable amount for order #{request.OrderId}.");
                }

                refund = new PaymentRefund
                {
                    ReturnRequestId = request.ReturnRequestId,
                    OrderId = request.OrderId,
                    Amount = request.Amount,
                    CustomerName = request.CustomerName,
                    EventId = request.EventId,
                    EventName = request.EventName,
                    Gateway = gateway,
                    GatewayRefundRef = reference,
                    Status = "Refunded",
                    RefundType = "Full",
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manual Bank Transfer" : request.Reason.Trim(),
                    ProcessedByUserId = request.ProcessedByUserId,
                    ProcessedByUserName = $"User #{request.ProcessedByUserId}",
                    GatewayAmount = isSimulation ? 0 : request.Amount,
                    ReconciliationStatus = isSimulation ? "Simulated" : "Matched",
                    CreatedAt = now,
                    CompletedAt = now
                };
                await _refunds.AddAsync(refund, ct);
            }
            else
            {
                refund.Amount = request.Amount;
                refund.Gateway = gateway;
                refund.GatewayRefundRef = reference;
                refund.Status = "Refunded";
                refund.ErrorMessage = null;
                refund.GatewayErrorCode = null;
                refund.GatewayAmount = isSimulation ? 0 : request.Amount;
                refund.ReconciliationStatus = isSimulation ? "Simulated" : "Matched";
                refund.ProcessedByUserId = request.ProcessedByUserId;
                refund.ProcessedByUserName = $"User #{request.ProcessedByUserId}";
                refund.CompletedAt = now;
                if (!string.IsNullOrWhiteSpace(request.CustomerName)) refund.CustomerName = request.CustomerName;
            }

            await _refunds.SaveChangesAsync(ct);
            return new RefundResultDto { RefundId = refund.PaymentRefundId, Status = "Refunded", Reference = refund.GatewayRefundRef };
        }

        // Process Full / Partial Refund
        public async Task<RefundResultDto> ProcessRefundAsync(ProcessRefundRequestDto request, int actorId, string actorName, CancellationToken ct = default)
        {
            if (request.OrderId <= 0)
                throw new ArgumentException("Valid order ID is required.");
            if (request.Amount <= 0)
                throw new ArgumentException("Refund amount must be greater than zero.");

            var payment = await _payments.GetLatestByOrderIdAsync(request.OrderId, "VNPay");
            if (payment == null || payment.Status != "Success")
                throw new InvalidOperationException("No successful payment transaction found for this order.");

            var alreadyRefunded = await _refunds.GetRefundedAmountByOrderIdAsync(request.OrderId, ct);

            var maxRefundable = payment.Amount - alreadyRefunded;
            if (request.Amount > maxRefundable)
            {
                // EX-01: Refund Amount Exceeds Limit
                throw new ArgumentException($"Refund amount exceeds the maximum refundable amount (â‚«{maxRefundable:N0}).");
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

            await _refunds.AddAsync(refund, ct);
            await _refunds.SaveChangesAsync(ct);

            return await ExecuteRefundAsync(refund, request.OrderId, request.Amount, ct);
        }

        // Retry Failed Refund
        public async Task<RefundResultDto> RetryRefundAsync(int refundId, string strategy, int actorId, string actorName, CancellationToken ct = default)
        {
            var refund = await _refunds.GetByIdAsync(refundId, ct: ct)
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
            await _refunds.SaveChangesAsync(ct);

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
                var (ok, reference, error, errCode) = await CallVnPayRefundAsync(payment, refund, amount, ct);
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
            await _refunds.SaveChangesAsync(ct);

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
            await _refunds.SaveChangesAsync(ct);

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

            var refunds = await _refunds.GetByOrderIdAsync(orderId, ct);

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

        // Refund Reconciliation Report
        public async Task<RefundReconciliationReportDto> GetReconciliationReportAsync(ReconciliationFilterDto filter, CancellationToken ct = default)
        {
            var allRefunds = await _refunds.GetReconciliationRefundsAsync(filter, ct);
            var availableEvents = await _refunds.GetDistinctRefundEventsAsync(ct);

            // Calculate KPI metrics
            var realRefunds = allRefunds.Where(r => r.Gateway != "SimulatedBankTransfer").ToList();
            var totalRefunds = realRefunds.Count;
            var totalAmount = realRefunds.Where(r => r.Status == "Refunded").Sum(r => r.Amount);
            var pendingCount = allRefunds.Count(r => r.Status == "Pending");
            var mismatchedCount = allRefunds.Count(r => r.ReconciliationStatus == "Mismatched" || r.Status == "Failed");

            return new RefundReconciliationReportDto
            {
                TotalRefunds = totalRefunds,
                SimulatedRefunds = allRefunds.Count - realRefunds.Count,
                TotalAmount = totalAmount,
                PendingReconciliation = pendingCount,
                MismatchedCount = mismatchedCount,
                Refunds = allRefunds.Select(MapRefundItem).ToList(),
                AvailableEvents = availableEvents
            };
        }

        public async Task<RefundItemDto?> GetRefundByIdAsync(int refundId, CancellationToken ct = default)
        {
            var refund = await _refunds.GetByIdAsync(refundId, asNoTracking: true, ct: ct);
            return refund == null ? null : MapRefundItem(refund);
        }

        private static RefundItemDto MapRefundItem(PaymentRefund r)
        {
            var simulation = r.Gateway == "SimulatedBankTransfer";
            var displayStatus = simulation ? "Simulated" : r.Status switch
            {
                "Refunded" => "Completed",
                "Pending" => "Pending",
                "Failed" => "Failed",
                _ => r.Status
            };

            var matchStatus = simulation ? "Simulated" : r.Status switch
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
            PaymentTransaction payment, PaymentRefund refund, long amount, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(payment.GatewayTransactionRef))
                return (false, null, "The original VNPay transaction number is missing.", "E0001");

            var payDate = ReadPayDate(payment.WebhookPayload);
            if (payDate == null)
                return (false, null, "The original VNPay payment date is missing.", "E0002");

            // VNPay treats vnp_RequestId as the idempotency key. Reusing the id of the
            // refund record means a retry asks about the same transaction instead of
            // creating a second payout for the same return.
            var requestId = $"RFD{refund.PaymentRefundId:D10}";
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

