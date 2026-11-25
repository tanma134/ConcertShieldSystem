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
        int? orderId,
        int page,
        int pageSize)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                throw new ArgumentException(
                    "From must be earlier than or equal to To.");
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var transactions =
                await _repository.GetAllForReconciliationAsync();

            var resolvedTransactionIds =
                await _resolutionRepository.GetResolvedPaymentTransactionIdsAsync();

            var exceptions =
                new List<PaymentReconciliationExceptionDto>();

            foreach (var group in transactions.GroupBy(t => t.OrderId))
            {
                var attempts = group
                    .OrderBy(t => t.RequestedAt)
                    .ThenBy(t => t.PaymentTransactionId)
                    .ToList();

                if (attempts.Count == 0)
                    continue;

                var latest = attempts[^1];

                if (orderId.HasValue && latest.OrderId != orderId.Value)
                    continue;

                if (resolvedTransactionIds.Contains(latest.PaymentTransactionId))
                    continue;

                if (from.HasValue && latest.RequestedAt < from.Value)
                    continue;

                if (to.HasValue && latest.RequestedAt > to.Value)
                    continue;

                var successfulAttempts = attempts
                    .Where(t => string.Equals(
                        t.Status,
                        "Success",
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var order =
                    await _ticketApiClient.GetOrderReconciliationInfoAsync(
                        latest.OrderId);

                // Mặc định dùng transaction mới nhất.
                // Nếu order Paid đang thiếu vé/QR, chọn một payment Success
                // hợp lệ để hành động Repair có thể xác thực được transaction.
                var transactionForResolution = latest;

                if (order != null)
                {
                    var orderIsPaid = string.Equals(
                        order.Status,
                        "Paid",
                        StringComparison.OrdinalIgnoreCase);

                    var hasMissingArtifacts =
                        order.IssuedTicketCount < order.ExpectedTicketCount ||
                        order.TicketsWithoutQrCount > 0;

                    if (orderIsPaid && hasMissingArtifacts)
                    {
                        var validSuccess = successfulAttempts
                            .Where(t =>
                                !resolvedTransactionIds.Contains(
                                    t.PaymentTransactionId) &&
                                t.Amount == order.FinalAmount &&
                                !string.IsNullOrWhiteSpace(
                                    t.GatewayTransactionRef) &&
                                string.Equals(
                                    t.GatewayTransactionRef,
                                    order.PaymentGatewayRef,
                                    StringComparison.Ordinal))
                            .OrderBy(t => t.RequestedAt)
                            .ThenBy(t => t.PaymentTransactionId)
                            .FirstOrDefault();

                        if (validSuccess != null)
                            transactionForResolution = validSuccess;
                    }
                }

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

                    if (orderIsPaid &&
                        order.IssuedTicketCount < order.ExpectedTicketCount)
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

                    if (!string.Equals(
                            latest.Status,
                            "Failed",
                            StringComparison.OrdinalIgnoreCase) &&
                        latest.Amount != order.FinalAmount)
                    {
                        reasons.Add(
                            $"Payment amount ({latest.Amount}) does not match order amount ({order.FinalAmount}).");
                    }

                    if (string.Equals(
                            latest.Status,
                            "Success",
                            StringComparison.OrdinalIgnoreCase) &&
                        !orderIsPaid)
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

                    if (string.Equals(
                            latest.Status,
                            "Failed",
                            StringComparison.OrdinalIgnoreCase) &&
                        orderIsPaid &&
                        successfulAttempts.Count == 0)
                    {
                        reasons.Add(
                            "Payment failed, but the order is marked as Paid.");
                    }

                    if (string.Equals(
                            latest.Status,
                            "Pending",
                            StringComparison.OrdinalIgnoreCase) &&
                        orderIsPaid)
                    {
                        reasons.Add(
                            "Order is Paid, but the latest payment transaction is still Pending.");
                    }
                    else if (
                        string.Equals(
                            latest.Status,
                            "Pending",
                            StringComparison.OrdinalIgnoreCase) &&
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
                    PaymentTransactionId =
                        transactionForResolution.PaymentTransactionId,
                    OrderId = transactionForResolution.OrderId,
                    EventId = order?.EventId,
                    Gateway = transactionForResolution.Gateway,
                    GatewayTransactionRef =
                        transactionForResolution.GatewayTransactionRef,
                    PaymentAmount = transactionForResolution.Amount,
                    OrderAmount = order?.FinalAmount,
                    PaymentStatus = transactionForResolution.Status,
                    OrderStatus = order?.Status,
                    Reasons = reasons,
                    RequestedAt = transactionForResolution.RequestedAt,
                    RespondedAt = transactionForResolution.RespondedAt,
                    ExpectedTicketCount = order?.ExpectedTicketCount ?? 0,
                    IssuedTicketCount = order?.IssuedTicketCount ?? 0,
                    TicketsWithoutQrCount = order?.TicketsWithoutQrCount ?? 0
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
                throw new ArgumentException("Invalid payment transaction ID.");

            if (string.IsNullOrWhiteSpace(request.ResolutionNote))
                throw new ArgumentException("Please enter a resolution note.");

            var action = request.ResolutionAction?.Trim();
            var resolutionStatus = request.ResolutionStatus?.Trim();

            if (action != "ConfirmPayment" &&
                action != "RepairPaidOrderArtifacts" &&
                action != "ManualReview")
            {
                throw new ArgumentException("Invalid resolution action.");
            }

            if (resolutionStatus != "Resolved" && resolutionStatus != "Dismissed")
                throw new ArgumentException("Invalid resolution status.");

            if (action != "ManualReview" && resolutionStatus != "Resolved")
                throw new ArgumentException(
                    "Automated actions can only be marked as Resolved.");

            var transaction =
                await _repository.GetByIdAsync(paymentTransactionId);

            if (transaction == null)
                throw new KeyNotFoundException(
                    "Payment transaction not found.");

            if (await _resolutionRepository.ExistsByPaymentTransactionIdAsync(
                    paymentTransactionId))
            {
                throw new InvalidOperationException(
                    "This exception has already been resolved.");
            }

            if (action != "ManualReview")
            {
                if (!string.Equals(
                        transaction.Status,
                        "Success",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Automated actions are only allowed for transactions confirmed as successful.");
                }

                if (string.IsNullOrWhiteSpace(transaction.GatewayTransactionRef))
                    throw new InvalidOperationException(
                        "The transaction has no gateway reference to verify.");

                var order =
                    await _ticketApiClient.GetOrderReconciliationInfoAsync(
                        transaction.OrderId);

                if (order == null)
                    throw new KeyNotFoundException(
                        "Order not found in TicketAPI.");

                if (transaction.Amount != order.FinalAmount)
                    throw new InvalidOperationException(
                        "The payment amount does not match the order's final amount.");

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
                            "ConfirmPayment only applies to Pending orders.");
                    }

                    var confirmed =
                        await _ticketApiClient.ConfirmOrderPaymentAsync(
                            transaction.OrderId,
                            paymentRequest);

                    if (!confirmed)
                        throw new InvalidOperationException(
                            "TicketAPI could not confirm payment for the order.");
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
                            "RepairPaidOrderArtifacts only applies to Paid orders with missing tickets or QR codes.");
                    }

                    var repaired =
                        await _ticketApiClient.RepairPaidOrderArtifactsAsync(
                            transaction.OrderId,
                            paymentRequest);

                    if (!repaired)
                        throw new InvalidOperationException(
                            "TicketAPI could not restore tickets or QR codes for the order.");
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

        public async Task<PaymentReconciliationExceptionPageDto>
    GetFailedTransactionQueueAsync(
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                throw new ArgumentException(
                    "From must be earlier than or equal to To.");
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var transactions =
                await _repository.GetAllForReconciliationAsync();

            var resolvedTransactionIds =
                await _resolutionRepository.GetResolvedPaymentTransactionIdsAsync();

            var failedItems = transactions
                .Where(transaction =>
                    string.Equals(
                        transaction.Status,
                        "Failed",
                        StringComparison.OrdinalIgnoreCase))
                .Where(transaction =>
                    !resolvedTransactionIds.Contains(
                        transaction.PaymentTransactionId))
                .Where(transaction =>
                    !from.HasValue || transaction.RequestedAt >= from.Value)
                .Where(transaction =>
                    !to.HasValue || transaction.RequestedAt <= to.Value)
                .OrderByDescending(transaction => transaction.RequestedAt)
                .Select(transaction => new PaymentReconciliationExceptionDto
                {
                    PaymentTransactionId = transaction.PaymentTransactionId,
                    OrderId = transaction.OrderId,
                    EventId = null,
                    Gateway = transaction.Gateway,
                    GatewayTransactionRef = transaction.GatewayTransactionRef,
                    PaymentAmount = transaction.Amount,
                    OrderAmount = null,
                    PaymentStatus = transaction.Status,
                    OrderStatus = null,
                    Reasons = new List<string>
                    {
                "Payment transaction failed."
                    },
                    RequestedAt = transaction.RequestedAt,
                    RespondedAt = transaction.RespondedAt,
                    ExpectedTicketCount = 0,
                    IssuedTicketCount = 0,
                    TicketsWithoutQrCount = 0
                })
                .ToList();

            return new PaymentReconciliationExceptionPageDto
            {
                Items = failedItems
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = failedItems.Count
            };
        }

        public async Task<TicketIssuanceRecoveryPageDto>
            GetTicketIssuanceRecoveryQueueAsync(
                DateTime? from,
                DateTime? to,
                int page,
                int pageSize)
        {
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                throw new ArgumentException(
                    "From must be earlier than or equal to To.");
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var transactions =
                await _repository.GetAllForReconciliationAsync();

            var resolvedTransactionIds =
                await _resolutionRepository.GetResolvedPaymentTransactionIdsAsync();

            var recoveryItems = new List<TicketIssuanceRecoveryDto>();

            foreach (var orderGroup in transactions.GroupBy(
                         transaction => transaction.OrderId))
            {
                var candidatePayments = orderGroup
                    .Where(transaction =>
                        string.Equals(
                            transaction.Status,
                            "Success",
                            StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(
                            transaction.GatewayTransactionRef) &&
                        !resolvedTransactionIds.Contains(
                            transaction.PaymentTransactionId) &&
                        (!from.HasValue || transaction.RequestedAt >= from.Value) &&
                        (!to.HasValue || transaction.RequestedAt <= to.Value))
                    .ToList();

                if (candidatePayments.Count == 0)
                    continue;

                var order =
                    await _ticketApiClient.GetOrderReconciliationInfoAsync(
                        orderGroup.Key);

                if (order == null ||
                    !string.Equals(
                        order.Status,
                        "Paid",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var missingTicketCount = Math.Max(
                    0,
                    order.ExpectedTicketCount - order.IssuedTicketCount);

                if (missingTicketCount == 0 && order.TicketsWithoutQrCount == 0)
                    continue;

                // Chỉ đưa vào queue payment Success khớp đúng số tiền và
                // gateway reference mà TicketAPI đã lưu trên order.
                var matchingPayment = candidatePayments
                    .Where(transaction =>
                        transaction.Amount == order.FinalAmount &&
                        string.Equals(
                            transaction.GatewayTransactionRef,
                            order.PaymentGatewayRef,
                            StringComparison.Ordinal))
                    .OrderByDescending(transaction => transaction.RequestedAt)
                    .ThenByDescending(transaction => transaction.PaymentTransactionId)
                    .FirstOrDefault();

                if (matchingPayment == null)
                    continue;

                var reasons = new List<string>();

                if (missingTicketCount > 0)
                {
                    reasons.Add(
                        $"Order is Paid, but only {order.IssuedTicketCount} of " +
                        $"{order.ExpectedTicketCount} tickets have been issued.");
                }

                if (order.TicketsWithoutQrCount > 0)
                {
                    reasons.Add(
                        $"{order.TicketsWithoutQrCount} issued ticket(s) have no QR token.");
                }

                recoveryItems.Add(new TicketIssuanceRecoveryDto
                {
                    PaymentTransactionId = matchingPayment.PaymentTransactionId,
                    OrderId = matchingPayment.OrderId,
                    EventId = order.EventId,
                    Gateway = matchingPayment.Gateway,
                    GatewayTransactionRef = matchingPayment.GatewayTransactionRef,
                    PaymentAmount = matchingPayment.Amount,
                    OrderAmount = order.FinalAmount,
                    PaymentStatus = matchingPayment.Status,
                    OrderStatus = order.Status!,
                    Reasons = reasons,
                    RequestedAt = matchingPayment.RequestedAt,
                    RespondedAt = matchingPayment.RespondedAt,
                    ExpectedTicketCount = order.ExpectedTicketCount,
                    IssuedTicketCount = order.IssuedTicketCount,
                    TicketsWithoutQrCount = order.TicketsWithoutQrCount
                });
            }

            var orderedItems = recoveryItems
                .OrderByDescending(item => item.RequestedAt)
                .ThenByDescending(item => item.PaymentTransactionId)
                .ToList();

            return new TicketIssuanceRecoveryPageDto
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
