//using Moq;
//using PaymentAPI.API;
//using PaymentAPI.DTOs;
//using PaymentAPI.Models;
//using PaymentAPI.Repositories;
//using PaymentAPI.Services;
//using Xunit;

//namespace PaymentAPI.Tests;

//public class PaymentReconciliationExceptionsServiceTests
//{
//    private const long OrderAmount = 1000;

//    [Fact]
//    public async Task T01_GetExceptionsAsync_FromAfterTo_ThrowsArgumentException()
//    {
//        var (service, repository, _, _) = CreateService();

//        await Assert.ThrowsAsync<ArgumentException>(() =>
//            service.GetExceptionsAsync(
//                new DateTime(2026, 10, 20),
//                new DateTime(2026, 10, 19),
//                1,
//                20));

//        repository.Verify(
//            r => r.GetAllForReconciliationAsync(),
//            Times.Never);
//    }

//    [Fact]
//    public async Task T02_GetExceptionsAsync_PageAndPageSizeBelowMinimum_AreClampedToOne()
//    {
//        var (service, _, _, _) = CreateService();

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 0,
//            pageSize: 0);

//        Assert.Equal(1, result.Page);
//        Assert.Equal(1, result.PageSize);
//    }

//    [Fact]
//    public async Task T03_GetExceptionsAsync_PageSizeAboveMaximum_IsClampedToOneHundred()
//    {
//        var (service, _, _, _) = CreateService();

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 5,
//            pageSize: 101);

//        Assert.Equal(5, result.Page);
//        Assert.Equal(100, result.PageSize);
//    }

//    [Fact]
//    public async Task T04_GetExceptionsAsync_NoTransactions_ReturnsEmptyPage()
//    {
//        var (service, _, ticketApi, _) = CreateService();

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20);

//        Assert.Empty(result.Items);
//        Assert.Equal(0, result.TotalCount);
//        ticketApi.Verify(
//            c => c.GetOrderReconciliationInfoAsync(It.IsAny<int>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task T05_GetExceptionsAsync_ResolvedLatestTransaction_IsExcluded()
//    {
//        var transaction = CreateTransaction(
//            transactionId: 101,
//            orderId: 42,
//            status: "Pending");

//        var (service, _, ticketApi, _) = CreateService(
//            transactions: new[] { transaction },
//            resolvedIds: new HashSet<int> { 101 });

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20);

//        Assert.Empty(result.Items);
//        Assert.Equal(0, result.TotalCount);
//        ticketApi.Verify(
//            c => c.GetOrderReconciliationInfoAsync(It.IsAny<int>()),
//            Times.Never);
//    }

//    [Fact]
//    public async Task T06_GetExceptionsAsync_DateRangeIncludesOnlyTransactionsWithinRange()
//    {
//        var before = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
//        var inside = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
//        var after = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

//        var transactions = new[]
//        {
//            CreateTransaction(1, 101, requestedAt: before),
//            CreateTransaction(2, 102, requestedAt: inside),
//            CreateTransaction(3, 103, requestedAt: after)
//        };

//        var (service, _, _, _) = CreateService(transactions: transactions);

//        var result = await service.GetExceptionsAsync(
//            from: inside,
//            to: inside,
//            page: 1,
//            pageSize: 20);

//        var item = Assert.Single(result.Items);
//        Assert.Equal(2, item.PaymentTransactionId);
//        Assert.Equal(1, result.TotalCount);
//    }

//    [Fact]
//    public async Task T07_GetExceptionsAsync_UsesLatestAttemptForEachOrder()
//    {
//        var olderAttempt = CreateTransaction(
//            transactionId: 201,
//            orderId: 42,
//            status: "Failed",
//            requestedAt: DateTime.UtcNow.AddMinutes(-10));

//        var latestAttempt = CreateTransaction(
//            transactionId: 202,
//            orderId: 42,
//            status: "Pending",
//            requestedAt: DateTime.UtcNow.AddMinutes(-1));

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Paid")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { latestAttempt, olderAttempt },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Equal(202, item.PaymentTransactionId);
//        Assert.Equal("Pending", item.PaymentStatus);
//        Assert.Contains(
//            "Order is Paid, but the latest payment transaction is still Pending.",
//            item.Reasons);
//    }

//    [Fact]
//    public async Task T08_GetExceptionsAsync_OrderNotFound_ReturnsException()
//    {
//        var transaction = CreateTransaction(301, 42);

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction });

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Equal(42, item.OrderId);
//        Assert.Contains("Order was not found in TicketAPI.", item.Reasons);
//        Assert.Null(item.EventId);
//        Assert.Null(item.OrderAmount);
//    }

//    [Fact]
//    public async Task T09_GetExceptionsAsync_PaidOrderWithMissingIssuedTickets_ReturnsException()
//    {
//        var transaction = CreateTransaction(401, 42, status: "Success");

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(
//                42,
//                status: "Paid",
//                expectedTickets: 3,
//                issuedTickets: 1)
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Contains(
//            "Order is Paid, but only 1 of 3 tickets have been issued.",
//            item.Reasons);
//    }

//    [Fact]
//    public async Task T10_GetExceptionsAsync_PaidOrderWithTicketMissingQr_ReturnsException()
//    {
//        var transaction = CreateTransaction(501, 42, status: "Success");

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(
//                42,
//                status: "Paid",
//                ticketsWithoutQr: 2)
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Contains("2 issued ticket(s) have no QR token.", item.Reasons);
//    }

//    [Fact]
//    public async Task T11_GetExceptionsAsync_NonFailedPaymentAmountMismatch_ReturnsException()
//    {
//        var transaction = CreateTransaction(
//            601,
//            42,
//            status: "Pending",
//            amount: 900);

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(
//                42,
//                status: "Pending",
//                finalAmount: 1000)
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Contains(
//            "Payment amount (900) does not match order amount (1000).",
//            item.Reasons);
//    }

//    [Fact]
//    public async Task T12_OldSuccessfulPaymentWithUnpaidOrder_ReturnsException()
//    {
//        var transaction = CreateTransaction(
//            701,
//            42,
//            status: "Success",
//            requestedAt: DateTime.UtcNow.AddMinutes(-10),
//            respondedAt: DateTime.UtcNow.AddMinutes(-5));

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Pending")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Contains(
//            "Payment succeeded, but order status is 'Pending'.",
//            item.Reasons);
//    }

//    [Fact]
//    public async Task T13_RecentSuccessfulPaymentWithUnpaidOrder_IsWithinGracePeriod()
//    {
//        var transaction = CreateTransaction(
//            801,
//            42,
//            status: "Success",
//            requestedAt: DateTime.UtcNow.AddMinutes(-1),
//            respondedAt: DateTime.UtcNow.AddMinutes(-1));

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Pending")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20);

//        Assert.Empty(result.Items);
//    }

//    [Fact]
//    public async Task T14_FailedPaymentWithPaidOrderAndNoSuccessfulAttempt_ReturnsException()
//    {
//        var transaction = CreateTransaction(
//            901,
//            42,
//            status: "Failed");

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Paid")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Contains(
//            "Payment failed, but the order is marked as Paid.",
//            item.Reasons);
//    }

//    [Fact]
//    public async Task T15_FailedPaymentWithEarlierSuccess_DoesNotAddFailedPaidReason()
//    {
//        var earlierSuccess = CreateTransaction(
//            1001,
//            42,
//            status: "Success",
//            requestedAt: DateTime.UtcNow.AddMinutes(-10));

//        var latestFailure = CreateTransaction(
//            1002,
//            42,
//            status: "Failed",
//            requestedAt: DateTime.UtcNow.AddMinutes(-5));

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Paid")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { earlierSuccess, latestFailure },
//            orders: orders);

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20);

//        Assert.Empty(result.Items);
//    }

//    [Fact]
//    public async Task T16_PendingPaymentWithPaidOrder_ReturnsException()
//    {
//        var transaction = CreateTransaction(
//            1101,
//            42,
//            status: "Pending");

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Paid")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Contains(
//            "Order is Paid, but the latest payment transaction is still Pending.",
//            item.Reasons);
//    }

//    [Fact]
//    public async Task T17_PendingPaymentBeyondTimeoutWithoutSuccess_ReturnsException()
//    {
//        var transaction = CreateTransaction(
//            1201,
//            42,
//            status: "Pending",
//            requestedAt: DateTime.UtcNow.AddMinutes(-20));

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Pending")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Contains(
//            "Payment transaction has remained Pending beyond the timeout.",
//            item.Reasons);
//    }

//    [Fact]
//    public async Task T18_OldPendingAttemptWithEarlierSuccess_DoesNotAddTimeoutReason()
//    {
//        var earlierSuccess = CreateTransaction(
//            1301,
//            42,
//            status: "Success",
//            requestedAt: DateTime.UtcNow.AddMinutes(-30));

//        var latestPending = CreateTransaction(
//            1302,
//            42,
//            status: "Pending",
//            requestedAt: DateTime.UtcNow.AddMinutes(-20));

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Pending")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { earlierSuccess, latestPending },
//            orders: orders);

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20);

//        Assert.Empty(result.Items);
//    }

//    [Fact]
//    public async Task T19_MoreThanOneSuccessfulAttempt_ReturnsException()
//    {
//        var firstSuccess = CreateTransaction(
//            1401,
//            42,
//            status: "Success",
//            requestedAt: DateTime.UtcNow.AddMinutes(-10));

//        var secondSuccess = CreateTransaction(
//            1402,
//            42,
//            status: "Success",
//            requestedAt: DateTime.UtcNow.AddMinutes(-5));

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Paid")
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { firstSuccess, secondSuccess },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Contains(
//            "More than one successful payment transaction exists for this order.",
//            item.Reasons);
//    }

//    [Fact]
//    public async Task T20_FailedPaymentAmountMismatch_DoesNotAddAmountMismatchReason()
//    {
//        var transaction = CreateTransaction(
//            1501,
//            42,
//            status: "Failed",
//            amount: 900);

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(42, status: "Pending", finalAmount: 1000)
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20);

//        Assert.Empty(result.Items);
//    }

//    [Fact]
//    public async Task T21_MultipleReconciliationProblems_AreAllReturned()
//    {
//        var transaction = CreateTransaction(
//            1601,
//            42,
//            status: "Pending",
//            amount: 900);

//        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
//        {
//            [42] = CreateOrderInfo(
//                42,
//                status: "Paid",
//                finalAmount: 1000,
//                expectedTickets: 2,
//                issuedTickets: 1,
//                ticketsWithoutQr: 1)
//        };

//        var (service, _, _, _) = CreateService(
//            transactions: new[] { transaction },
//            orders: orders);

//        var item = Assert.Single((await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 1,
//            pageSize: 20)).Items);

//        Assert.Equal(4, item.Reasons.Count);
//        Assert.Contains(item.Reasons, reason => reason.Contains("only 1 of 2"));
//        Assert.Contains(item.Reasons, reason => reason.Contains("no QR token"));
//        Assert.Contains(item.Reasons, reason => reason.Contains("does not match"));
//        Assert.Contains(item.Reasons, reason => reason.Contains("still Pending"));
//    }

//    [Fact]
//    public async Task T22_ExceptionsAreSortedNewestFirstAndPaginated()
//    {
//        var transactions = new[]
//        {
//            CreateTransaction(1701, 101, requestedAt: DateTime.UtcNow.AddHours(-3)),
//            CreateTransaction(1702, 102, requestedAt: DateTime.UtcNow.AddHours(-2)),
//            CreateTransaction(1703, 103, requestedAt: DateTime.UtcNow.AddHours(-1))
//        };

//        var (service, _, _, _) = CreateService(transactions: transactions);

//        var result = await service.GetExceptionsAsync(
//            null,
//            null,
//            page: 2,
//            pageSize: 1);

//        var item = Assert.Single(result.Items);
//        Assert.Equal(3, result.TotalCount);
//        Assert.Equal(2, result.Page);
//        Assert.Equal(1, result.PageSize);
//        Assert.Equal(1702, item.PaymentTransactionId);
//    }

//    [Fact]
//    public async Task T23_RepositoryFailure_IsPropagated()
//    {
//        var (service, repository, _, _) = CreateService();

//        repository
//            .Setup(r => r.GetAllForReconciliationAsync())
//            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

//        await Assert.ThrowsAsync<InvalidOperationException>(() =>
//            service.GetExceptionsAsync(
//                null,
//                null,
//                page: 1,
//                pageSize: 20));
//    }

//    private static (
//        PaymentReconciliationService Service,
//        Mock<IPaymentTransactionRepository> Repository,
//        Mock<ITicketApiClient> TicketApi,
//        Mock<IPaymentReconciliationResolutionRepository> Resolutions)
//        CreateService(
//            IEnumerable<PaymentTransaction>? transactions = null,
//            HashSet<int>? resolvedIds = null,
//            Dictionary<int, TicketOrderReconciliationInfoDto?>? orders = null,
//            Mock<IPaymentTransactionRepository>? repository = null)
//    {
//        repository ??= new Mock<IPaymentTransactionRepository>();

//        var ticketApi = new Mock<ITicketApiClient>();
//        var resolutions =
//            new Mock<IPaymentReconciliationResolutionRepository>();

//        repository
//            .Setup(r => r.GetAllForReconciliationAsync())
//            .ReturnsAsync(transactions?.ToList() ?? new List<PaymentTransaction>());

//        resolutions
//            .Setup(r => r.GetResolvedPaymentTransactionIdsAsync())
//            .ReturnsAsync(resolvedIds ?? new HashSet<int>());

//        orders ??= new Dictionary<int, TicketOrderReconciliationInfoDto?>();

//        ticketApi
//            .Setup(c => c.GetOrderReconciliationInfoAsync(It.IsAny<int>()))
//            .Returns((int orderId) =>
//                Task.FromResult(orders.GetValueOrDefault(orderId)));

//        var service = new PaymentReconciliationService(
//            repository.Object,
//            ticketApi.Object,
//            resolutions.Object);

//        return (service, repository, ticketApi, resolutions);
//    }

//    private static PaymentTransaction CreateTransaction(
//        int transactionId,
//        int orderId,
//        string status = "Pending",
//        long amount = OrderAmount,
//        DateTime? requestedAt = null,
//        DateTime? respondedAt = null)
//    {
//        return new PaymentTransaction
//        {
//            PaymentTransactionId = transactionId,
//            OrderId = orderId,
//            Gateway = "VNPay",
//            GatewayTransactionRef = $"TX-{transactionId}",
//            Amount = amount,
//            Status = status,
//            RequestedAt = requestedAt ?? DateTime.UtcNow.AddMinutes(-1),
//            RespondedAt = respondedAt
//        };
//    }

//    private static TicketOrderReconciliationInfoDto CreateOrderInfo(
//        int orderId,
//        string status = "Pending",
//        long finalAmount = OrderAmount,
//        int expectedTickets = 1,
//        int issuedTickets = 1,
//        int ticketsWithoutQr = 0)
//    {
//        return new TicketOrderReconciliationInfoDto
//        {
//            OrderId = orderId,
//            EventId = 42,
//            Status = status,
//            FinalAmount = finalAmount,
//            ExpectedTicketCount = expectedTickets,
//            IssuedTicketCount = issuedTickets,
//            TicketsWithoutQrCount = ticketsWithoutQr
//        };
//    }
//}