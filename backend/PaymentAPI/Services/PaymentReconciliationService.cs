using PaymentAPI.API;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services
{
    public class PaymentReconciliationService : IPaymentReconciliationService
    {
        private static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(15);

        private static readonly TimeSpan ConfirmationGracePeriod = TimeSpan.FromMinutes(2);

        private readonly IPaymentTransactionRepository _repository;
        private readonly ITicketApiClient _ticketApiClient;
        private readonly IPaymentReconciliationResolutionRepository _resolutionRepository;

        public PaymentReconciliationService(
            IPaymentTransactionRepository repository,
            ITicketApiClient ticketApiClient, IPaymentReconciliationResolutionRepository resolutionRepository)
        {
            _repository = repository;
            _ticketApiClient = ticketApiClient;
            _resolutionRepository = resolutionRepository;
        }

        public async Task<PaymentReconciliationExceptionPageDto>
            GetExceptionsAsync(
                DateTime? from,
                DateTime? to,
                int page,
                int pageSize)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
                throw new ArgumentException(
                    "From must be earlier than or equal to To.");

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var transactions =
                await _repository.GetAllForReconciliationAsync();

            var resolvedTransactionIds =
                await _resolutionRepository
                    .GetResolvedPaymentTransactionIdsAsync();

            var exceptions =
                new List<PaymentReconciliationExceptionDto>();

            foreach (var group in transactions.GroupBy(t => t.OrderId))
            {
                var attempts = group
                    .OrderBy(t => t.RequestedAt)
                    .ToList();

                if (attempts.Count == 0)
                    continue;

                var latest = attempts[^1];

                if (resolvedTransactionIds.Contains(latest.PaymentTransactionId))
                    continue;

                if (from.HasValue && latest.RequestedAt < from.Value)
                    continue;

                if (to.HasValue && latest.RequestedAt > to.Value)
                    continue;

                var successfulAttempts = attempts
                    .Where(t => t.Status == "Success")
                    .ToList();

                var order =
                    await _ticketApiClient.GetOrderReconciliationInfoAsync(
                        latest.OrderId);

                var reasons = new List<string>();

                if (order == null)
                {
                    reasons.Add("Order was not found in TicketAPI.");
                }
                else
                {
                    var orderIsPaid = string.Equals(
                        order.Status,
                        "Paid",
                        StringComparison.OrdinalIgnoreCase);

                    if (orderIsPaid && order.IssuedTicketCount < order.ExpectedTicketCount)
                    {
                        reasons.Add(
                            $"Order is Paid, but only {order.IssuedTicketCount} of " +
                            $"{order.ExpectedTicketCount} tickets have been issued.");
                    }

                    if (orderIsPaid && order.TicketsWithoutQrCount > 0)
                    {
                        reasons.Add(
                            $"{order.TicketsWithoutQrCount} issued ticket(s) have no QR token.");
                    }

                    if (latest.Status != "Failed" &&
                        latest.Amount != order.FinalAmount)
                    {
                        reasons.Add(
                            $"Payment amount ({latest.Amount}) does not match order amount ({order.FinalAmount}).");
                    }

                    if (latest.Status == "Success" && !orderIsPaid)
                    {
                        var confirmationTime =
                            latest.RespondedAt ?? latest.RequestedAt;

                        if (confirmationTime <=
                            DateTime.UtcNow - ConfirmationGracePeriod)
                        {
                            reasons.Add(
                                $"Payment succeeded, but order status is '{order.Status}'.");
                        }
                    }

                    if (latest.Status == "Failed" &&
                        orderIsPaid &&
                        successfulAttempts.Count == 0)
                    {
                        reasons.Add(
                            "Payment failed, but the order is marked as Paid.");
                    }

                    if (latest.Status == "Pending" && orderIsPaid)
                    {
                        reasons.Add(
                            "Order is Paid, but the latest payment transaction is still Pending.");
                    }
                    else if (
                        latest.Status == "Pending" &&
                        latest.RequestedAt <= DateTime.UtcNow - PendingTimeout &&
                        successfulAttempts.Count == 0)
                    {
                        reasons.Add(
                            "Payment transaction has remained Pending beyond the timeout.");
                    }
                }

                if (successfulAttempts.Count > 1)
                {
                    reasons.Add(
                        "More than one successful payment transaction exists for this order.");
                }

                if (reasons.Count == 0)
                    continue;

                exceptions.Add(new PaymentReconciliationExceptionDto
                {
                    PaymentTransactionId = latest.PaymentTransactionId,
                    OrderId = latest.OrderId,
                    EventId = order?.EventId,
                    Gateway = latest.Gateway,
                    GatewayTransactionRef = latest.GatewayTransactionRef,
                    PaymentAmount = latest.Amount,
                    OrderAmount = order?.FinalAmount,
                    PaymentStatus = latest.Status,
                    OrderStatus = order?.Status,
                    Reasons = reasons,
                    RequestedAt = latest.RequestedAt,
                    RespondedAt = latest.RespondedAt,
                    ExpectedTicketCount = order?.ExpectedTicketCount ?? 0,
                    IssuedTicketCount = order?.IssuedTicketCount ?? 0,
                    TicketsWithoutQrCount = order?.TicketsWithoutQrCount ?? 0,
                });
            }

            var orderedExceptions = exceptions
                .OrderByDescending(item => item.RequestedAt)
                .ToList();

            return new PaymentReconciliationExceptionPageDto
            {
                Items = orderedExceptions
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = orderedExceptions.Count
            };
        }

        public async Task<PaymentReconciliationResolutionDto> ResolveExceptionAsync(
            int paymentTransactionId,
            ResolvePaymentReconciliationRequestDto request,
            int adminUserId)
        {
            if (paymentTransactionId <= 0)
                throw new ArgumentException("Payment transaction ID không hợp lệ.");

            if (string.IsNullOrWhiteSpace(request.ResolutionNote))
                throw new ArgumentException("Vui lòng nhập ghi chú xử lý.");

            var action = request.ResolutionAction?.Trim();
            var resolutionStatus = request.ResolutionStatus?.Trim();

            if (action != "ConfirmPayment" &&
                action != "RepairPaidOrderArtifacts" &&
                action != "ManualReview")
            {
                throw new ArgumentException("Resolution action không hợp lệ.");
            }

            if (resolutionStatus != "Resolved" && resolutionStatus != "Dismissed")
                throw new ArgumentException("Resolution status không hợp lệ.");

            if (action != "ManualReview" && resolutionStatus != "Resolved")
                throw new ArgumentException(
                    "Các thao tác tự động chỉ được đánh dấu Resolved.");

            var transaction =
                await _repository.GetByIdAsync(paymentTransactionId);

            if (transaction == null)
                throw new KeyNotFoundException(
                    "Không tìm thấy payment transaction.");

            if (await _resolutionRepository.ExistsByPaymentTransactionIdAsync(
                    paymentTransactionId))
            {
                throw new InvalidOperationException(
                    "Exception này đã được resolve trước đó.");
            }

            if (action != "ManualReview")
            {
                if (!string.Equals(
                        transaction.Status,
                        "Success",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Chỉ thao tác tự động được transaction đã xác nhận thanh toán thành công.");
                }

                if (string.IsNullOrWhiteSpace(transaction.GatewayTransactionRef))
                    throw new InvalidOperationException(
                        "Transaction chưa có gateway reference để xác minh.");

                var order =
                    await _ticketApiClient.GetOrderReconciliationInfoAsync(
                        transaction.OrderId);

                if (order == null)
                    throw new KeyNotFoundException(
                        "Không tìm thấy order trong TicketAPI.");

                if (transaction.Amount != order.FinalAmount)
                    throw new InvalidOperationException(
                        "Payment amount không khớp với FinalAmount của order.");

                var paymentRequest = new ConfirmOrderPaymentRequestDTO
                {
                    Amount = transaction.Amount,
                    TransactionRef = transaction.GatewayTransactionRef
                };

                if (action == "ConfirmPayment")
                {
                    if (!string.Equals(
                            order.Status,
                            "Pending",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "ConfirmPayment chỉ áp dụng cho order đang Pending.");
                    }

                    var confirmed =
                        await _ticketApiClient.ConfirmOrderPaymentAsync(
                            transaction.OrderId,
                            paymentRequest);

                    if (!confirmed)
                        throw new InvalidOperationException(
                            "TicketAPI không xác nhận được thanh toán cho order.");
                }
                else
                {
                    var isPaid = string.Equals(
                        order.Status,
                        "Paid",
                        StringComparison.OrdinalIgnoreCase);

                    var hasMissingArtifacts =
                        order.IssuedTicketCount < order.ExpectedTicketCount ||
                        order.TicketsWithoutQrCount > 0;

                    if (!isPaid || !hasMissingArtifacts)
                    {
                        throw new InvalidOperationException(
                            "RepairPaidOrderArtifacts chỉ áp dụng cho order Paid bị thiếu vé hoặc QR.");
                    }

                    var repaired =
                        await _ticketApiClient.RepairPaidOrderArtifactsAsync(
                            transaction.OrderId,
                            paymentRequest);

                    if (!repaired)
                        throw new InvalidOperationException(
                            "TicketAPI không khôi phục được vé/QR cho order.");
                }
            }

            var resolution = new PaymentReconciliationResolution
            {
                PaymentTransactionId = paymentTransactionId,
                ResolutionStatus = resolutionStatus!,
                ResolutionAction = action!,
                ResolutionNote = request.ResolutionNote.Trim(),
                ResolvedByUserId = adminUserId,
                ResolvedAt = DateTime.UtcNow
            };

            await _resolutionRepository.AddAsync(resolution);

            return new PaymentReconciliationResolutionDto
            {
                PaymentTransactionId = resolution.PaymentTransactionId,
                ResolutionStatus = resolution.ResolutionStatus,
                ResolutionAction = resolution.ResolutionAction,
                ResolutionNote = resolution.ResolutionNote,
                ResolvedByUserId = resolution.ResolvedByUserId,
                ResolvedAt = resolution.ResolvedAt
            };
        }

        public async Task<PaymentReconciliationExceptionPageDto>GetFailedTransactionQueueAsync(
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
                throw new ArgumentException("From must be earlier than or equal to To.");

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var transactions = await _repository.GetAllForReconciliationAsync();

            var resolvedTransactionIds = await _resolutionRepository.GetResolvedPaymentTransactionIdsAsync();

            var successfulTransactionsByOrder = transactions
                .Where(transaction => string.Equals(
                    transaction.Status,
                    "Success",
                    StringComparison.OrdinalIgnoreCase))
                .GroupBy(transaction => transaction.OrderId);

            var queueItems = new List<PaymentReconciliationExceptionDto>();

            foreach (var group in successfulTransactionsByOrder)
            {
                var payment = group
                    .OrderByDescending(transaction => transaction.RequestedAt)
                    .First();

                if (resolvedTransactionIds.Contains(payment.PaymentTransactionId))
                    continue;

                if (from.HasValue && payment.RequestedAt < from.Value)
                    continue;

                if (to.HasValue && payment.RequestedAt > to.Value)
                    continue;

                var order = await _ticketApiClient.GetOrderReconciliationInfoAsync(payment.OrderId);

                if (order == null)
                    continue;

                if (!string.Equals(
                        order.Status,
                        "Paid",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var missingTickets =
                    order.IssuedTicketCount < order.ExpectedTicketCount;

                var missingQr = order.TicketsWithoutQrCount > 0;

                // Queue này chỉ gồm order Paid bị thiếu vé hoặc thiếu QR.
                if (!missingTickets && !missingQr)
                    continue;

                var reasons = new List<string>();

                if (missingTickets)
                {
                    reasons.Add(
                        $"Order is Paid, but only {order.IssuedTicketCount} of " +
                        $"{order.ExpectedTicketCount} tickets have been issued.");
                }

                if (missingQr)
                {
                    reasons.Add(
                        $"{order.TicketsWithoutQrCount} issued ticket(s) have no QR token.");
                }

                queueItems.Add(new PaymentReconciliationExceptionDto
                {
                    PaymentTransactionId = payment.PaymentTransactionId,
                    OrderId = payment.OrderId,
                    EventId = order.EventId,
                    Gateway = payment.Gateway,
                    GatewayTransactionRef = payment.GatewayTransactionRef,
                    PaymentAmount = payment.Amount,
                    OrderAmount = order.FinalAmount,
                    PaymentStatus = payment.Status,
                    OrderStatus = order.Status,
                    Reasons = reasons,
                    RequestedAt = payment.RequestedAt,
                    RespondedAt = payment.RespondedAt,
                    ExpectedTicketCount = order.ExpectedTicketCount,
                    IssuedTicketCount = order.IssuedTicketCount,
                    TicketsWithoutQrCount = order.TicketsWithoutQrCount
                });
            }

            var orderedItems = queueItems
                .OrderByDescending(item => item.RequestedAt)
                .ToList();

            return new PaymentReconciliationExceptionPageDto
            {
                Items = orderedItems
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = orderedItems.Count
            };
        }
    }
}
