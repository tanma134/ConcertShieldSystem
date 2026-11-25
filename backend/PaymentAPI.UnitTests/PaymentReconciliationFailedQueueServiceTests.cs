using Moq;
using PaymentAPI.API;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests;

public class PaymentReconciliationFailedQueueServiceTests
{
    private const long PaymentAmount = 1000;

    [Fact]
    public async Task T01_FromAfterTo_ThrowsArgumentException()
    {
        var (service, repository, _, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetFailedTransactionQueueAsync(
                from: new DateTime(2026, 10, 20),
                to: new DateTime(2026, 10, 19),
                page: 1,
                pageSize: 10));

        repository.Verify(
            repository => repository.GetAllForReconciliationAsync(),
            Times.Never);
    }

    [Fact]
    public async Task T02_PageAndPageSizeBelowMinimum_AreClampedToOne()
    {
        var (service, _, _, _) = CreateService();

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 0,
            pageSize: 0);

        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task T03_PageSizeAboveMaximum_IsClampedToOneHundred()
    {
        var transactions = Enumerable.Range(1, 101)
            .Select(index => CreateTransaction(
                transactionId: index,
                orderId: 1000 + index,
                requestedAt: new DateTime(2026, 10, 1).AddMinutes(index)))
            .ToList();

        var orders = transactions.ToDictionary(
            transaction => transaction.OrderId,
            transaction => CreateOrderInfo(
                transaction.OrderId,
                expectedTickets: 1,
                issuedTickets: 0));

        var (service, _, _, _) = CreateService(
            transactions: transactions,
            orders: orders);

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 101);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, result.Items.Count);
        Assert.Equal(101, result.TotalCount);
    }

    [Fact]
    public async Task T04_NoTransactions_ReturnsEmptyQueue()
    {
        var (service, _, ticketApi, _) = CreateService();

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);

        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task T05_OnlySuccessfulPaymentsAreConsidered_CaseInsensitive()
    {
        var transactions = new[]
        {
            CreateTransaction(501, 51, status: "Success"),
            CreateTransaction(502, 52, status: "success"),
            CreateTransaction(503, 53, status: "Failed"),
            CreateTransaction(504, 54, status: "Pending")
        };

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [51] = CreateOrderInfo(51, expectedTickets: 2, issuedTickets: 1),
            [52] = CreateOrderInfo(52, expectedTickets: 2, issuedTickets: 1),
            [53] = CreateOrderInfo(53, expectedTickets: 2, issuedTickets: 1),
            [54] = CreateOrderInfo(54, expectedTickets: 2, issuedTickets: 1)
        };

        var (service, _, ticketApi, _) = CreateService(
            transactions: transactions,
            orders: orders);

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
            Assert.Contains(
                item.PaymentStatus,
                new[] { "Success", "success" }));

        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(51),
            Times.Once);
        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(52),
            Times.Once);
        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(53),
            Times.Never);
        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(54),
            Times.Never);
    }

    [Fact]
    public async Task T06_LatestSuccessfulAttemptForOrderIsUsed()
    {
        var olderSuccess = CreateTransaction(
            transactionId: 601,
            orderId: 60,
            status: "Success",
            requestedAt: new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc));

        var latestSuccess = CreateTransaction(
            transactionId: 602,
            orderId: 60,
            status: "Success",
            requestedAt: new DateTime(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc));

        // A later failed attempt is excluded before successful attempts are grouped.
        var laterFailure = CreateTransaction(
            transactionId: 603,
            orderId: 60,
            status: "Failed",
            requestedAt: new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [60] = CreateOrderInfo(
                60,
                expectedTickets: 2,
                issuedTickets: 1)
        };

        var (service, _, ticketApi, _) = CreateService(
            transactions: new[] { olderSuccess, latestSuccess, laterFailure },
            orders: orders);

        var item = Assert.Single((await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10)).Items);

        Assert.Equal(602, item.PaymentTransactionId);
        Assert.Equal(latestSuccess.RequestedAt, item.RequestedAt);
        Assert.Equal("Success", item.PaymentStatus);

        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(60),
            Times.Once);
    }

    [Fact]
    public async Task T07_ResolvedLatestSuccessfulTransaction_IsExcluded()
    {
        var transaction = CreateTransaction(
            transactionId: 701,
            orderId: 70);

        var (service, _, ticketApi, _) = CreateService(
            transactions: new[] { transaction },
            resolvedIds: new HashSet<int> { 701 });

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);

        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task T08_DateRangeIncludesBothBoundaries_AndExcludesOutsideTransactions()
    {
        var from = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        var transactions = new[]
        {
            CreateTransaction(801, 81, requestedAt: from.AddTicks(-1)),
            CreateTransaction(802, 82, requestedAt: from),
            CreateTransaction(803, 83, requestedAt: to),
            CreateTransaction(804, 84, requestedAt: to.AddTicks(1))
        };

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [81] = CreateOrderInfo(81, expectedTickets: 2, issuedTickets: 1),
            [82] = CreateOrderInfo(82, expectedTickets: 2, issuedTickets: 1),
            [83] = CreateOrderInfo(83, expectedTickets: 2, issuedTickets: 1),
            [84] = CreateOrderInfo(84, expectedTickets: 2, issuedTickets: 1)
        };

        var (service, _, ticketApi, _) = CreateService(
            transactions: transactions,
            orders: orders);

        var result = await service.GetFailedTransactionQueueAsync(
            from: from,
            to: to,
            page: 1,
            pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(
            new[] { 803, 802 },
            result.Items.Select(item => item.PaymentTransactionId));

        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(81),
            Times.Never);
        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(84),
            Times.Never);
    }

    [Fact]
    public async Task T09_OrderNotFoundInTicketApi_IsSkipped()
    {
        var transaction = CreateTransaction(901, 90);

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [90] = null
        };

        var (service, _, _, _) = CreateService(
            transactions: new[] { transaction },
            orders: orders);

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task T10_NonPaidOrderIsSkipped_AndPaidStatusIsCaseInsensitive()
    {
        var transactions = new[]
        {
            CreateTransaction(1001, 101),
            CreateTransaction(1002, 102)
        };

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [101] = CreateOrderInfo(
                101,
                status: "Pending",
                expectedTickets: 2,
                issuedTickets: 1),
            [102] = CreateOrderInfo(
                102,
                status: "paid",
                expectedTickets: 2,
                issuedTickets: 1)
        };

        var (service, _, _, _) = CreateService(
            transactions: transactions,
            orders: orders);

        var item = Assert.Single((await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10)).Items);

        Assert.Equal(1002, item.PaymentTransactionId);
        Assert.Equal("paid", item.OrderStatus);
    }

    [Fact]
    public async Task T11_OrderWithAllTicketsAndQrTokens_IsNotQueued()
    {
        var transaction = CreateTransaction(1101, 110);

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [110] = CreateOrderInfo(
                110,
                status: "Paid",
                expectedTickets: 2,
                issuedTickets: 2,
                ticketsWithoutQr: 0)
        };

        var (service, _, _, _) = CreateService(
            transactions: new[] { transaction },
            orders: orders);

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task T12_PaidOrderWithMissingTickets_IsQueuedWithCorrectReason()
    {
        var transaction = CreateTransaction(1201, 120);

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [120] = CreateOrderInfo(
                120,
                status: "Paid",
                expectedTickets: 3,
                issuedTickets: 1,
                ticketsWithoutQr: 0)
        };

        var (service, _, _, _) = CreateService(
            transactions: new[] { transaction },
            orders: orders);

        var item = Assert.Single((await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10)).Items);

        Assert.Equal(1201, item.PaymentTransactionId);
        Assert.Equal(120, item.OrderId);
        Assert.Equal(42, item.EventId);
        Assert.Equal("VNPay", item.Gateway);
        Assert.Equal("TX-1201", item.GatewayTransactionRef);
        Assert.Equal(PaymentAmount, item.PaymentAmount);
        Assert.Equal(PaymentAmount, item.OrderAmount);
        Assert.Equal("Success", item.PaymentStatus);
        Assert.Equal("Paid", item.OrderStatus);
        Assert.Equal(3, item.ExpectedTicketCount);
        Assert.Equal(1, item.IssuedTicketCount);
        Assert.Equal(0, item.TicketsWithoutQrCount);
        Assert.Equal(
            "Order is Paid, but only 1 of 3 tickets have been issued.",
            Assert.Single(item.Reasons));
    }

    [Fact]
    public async Task T13_PaidOrderWithMissingQr_IsQueuedWithCorrectReason()
    {
        var transaction = CreateTransaction(1301, 130);

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [130] = CreateOrderInfo(
                130,
                status: "Paid",
                expectedTickets: 2,
                issuedTickets: 2,
                ticketsWithoutQr: 1)
        };

        var (service, _, _, _) = CreateService(
            transactions: new[] { transaction },
            orders: orders);

        var item = Assert.Single((await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10)).Items);

        Assert.Equal(2, item.ExpectedTicketCount);
        Assert.Equal(2, item.IssuedTicketCount);
        Assert.Equal(1, item.TicketsWithoutQrCount);
        Assert.Equal(
            "1 issued ticket(s) have no QR token.",
            Assert.Single(item.Reasons));
    }

    [Fact]
    public async Task T14_PaidOrderWithMissingTicketsAndQr_ReturnsBothReasons()
    {
        var transaction = CreateTransaction(1401, 140);

        var orders = new Dictionary<int, TicketOrderReconciliationInfoDto?>
        {
            [140] = CreateOrderInfo(
                140,
                status: "Paid",
                expectedTickets: 3,
                issuedTickets: 1,
                ticketsWithoutQr: 2)
        };

        var (service, _, _, _) = CreateService(
            transactions: new[] { transaction },
            orders: orders);

        var item = Assert.Single((await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10)).Items);

        Assert.Equal(
            new[]
            {
                "Order is Paid, but only 1 of 3 tickets have been issued.",
                "2 issued ticket(s) have no QR token."
            },
            item.Reasons);
    }

    [Fact]
    public async Task T15_QueueItemsAreSortedNewestFirst()
    {
        var transactions = new[]
        {
            CreateTransaction(
                1501, 151,
                requestedAt: new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(
                1502, 152,
                requestedAt: new DateTime(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(
                1503, 153,
                requestedAt: new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc))
        };

        var orders = transactions.ToDictionary(
            transaction => transaction.OrderId,
            transaction => CreateOrderInfo(
                transaction.OrderId,
                expectedTickets: 2,
                issuedTickets: 1));

        var (service, _, _, _) = CreateService(
            transactions: transactions,
            orders: orders);

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 1,
            pageSize: 10);

        Assert.Equal(
            new[] { 1502, 1503, 1501 },
            result.Items.Select(item => item.PaymentTransactionId));
    }

    [Fact]
    public async Task T16_QueueIsPaginatedAfterSorting()
    {
        var transactions = new[]
        {
            CreateTransaction(
                1601, 161,
                requestedAt: new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(
                1602, 162,
                requestedAt: new DateTime(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(
                1603, 163,
                requestedAt: new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc))
        };

        var orders = transactions.ToDictionary(
            transaction => transaction.OrderId,
            transaction => CreateOrderInfo(
                transaction.OrderId,
                expectedTickets: 2,
                issuedTickets: 1));

        var (service, _, _, _) = CreateService(
            transactions: transactions,
            orders: orders);

        var result = await service.GetFailedTransactionQueueAsync(
            from: null,
            to: null,
            page: 2,
            pageSize: 1);

        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1603, Assert.Single(result.Items).PaymentTransactionId);
    }

    [Fact]
    public async Task T17_TransactionRepositoryFailure_IsPropagated()
    {
        var (service, repository, ticketApi, _) = CreateService();

        repository
            .Setup(item => item.GetAllForReconciliationAsync())
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetFailedTransactionQueueAsync(
                from: null,
                to: null,
                page: 1,
                pageSize: 10));

        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task T18_ResolutionRepositoryFailure_IsPropagated()
    {
        var (service, repository, ticketApi, resolutions) = CreateService();

        resolutions
            .Setup(item => item.GetResolvedPaymentTransactionIdsAsync())
            .ThrowsAsync(
                new InvalidOperationException("Resolution store unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetFailedTransactionQueueAsync(
                from: null,
                to: null,
                page: 1,
                pageSize: 10));

        repository.Verify(
            item => item.GetAllForReconciliationAsync(),
            Times.Once);

        ticketApi.Verify(
            client => client.GetOrderReconciliationInfoAsync(
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task T19_TicketApiFailure_IsPropagated()
    {
        var transaction = CreateTransaction(1901, 190);

        var (service, _, ticketApi, _) = CreateService(
            transactions: new[] { transaction });

        ticketApi
            .Setup(client => client.GetOrderReconciliationInfoAsync(190))
            .ThrowsAsync(new InvalidOperationException("TicketAPI unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetFailedTransactionQueueAsync(
                from: null,
                to: null,
                page: 1,
                pageSize: 10));
    }

    private static (
        PaymentReconciliationService Service,
        Mock<IPaymentTransactionRepository> Repository,
        Mock<ITicketApiClient> TicketApi,
        Mock<IPaymentReconciliationResolutionRepository> Resolutions)
        CreateService(
            IEnumerable<PaymentTransaction>? transactions = null,
            HashSet<int>? resolvedIds = null,
            Dictionary<int, TicketOrderReconciliationInfoDto?>? orders = null,
            Mock<IPaymentTransactionRepository>? repository = null,
            Mock<IPaymentReconciliationResolutionRepository>? resolutions = null)
    {
        repository ??= new Mock<IPaymentTransactionRepository>();
        resolutions ??=
            new Mock<IPaymentReconciliationResolutionRepository>();

        var ticketApi = new Mock<ITicketApiClient>();

        repository
            .Setup(item => item.GetAllForReconciliationAsync())
            .ReturnsAsync(
                transactions?.ToList() ?? new List<PaymentTransaction>());

        resolutions
            .Setup(item => item.GetResolvedPaymentTransactionIdsAsync())
            .ReturnsAsync(resolvedIds ?? new HashSet<int>());

        orders ??= new Dictionary<int, TicketOrderReconciliationInfoDto?>();

        ticketApi
            .Setup(client => client.GetOrderReconciliationInfoAsync(
                It.IsAny<int>()))
            .Returns((int orderId) =>
                Task.FromResult(orders.GetValueOrDefault(orderId)));

        var service = new PaymentReconciliationService(
            repository.Object,
            ticketApi.Object,
            resolutions.Object);

        return (service, repository, ticketApi, resolutions);
    }

    private static PaymentTransaction CreateTransaction(
        int transactionId,
        int orderId,
        string status = "Success",
        long amount = PaymentAmount,
        DateTime? requestedAt = null,
        DateTime? respondedAt = null)
    {
        return new PaymentTransaction
        {
            PaymentTransactionId = transactionId,
            OrderId = orderId,
            Gateway = "VNPay",
            GatewayTransactionRef = $"TX-{transactionId}",
            Amount = amount,
            Status = status,
            RequestedAt = requestedAt ??
                          new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            RespondedAt = respondedAt
        };
    }

    private static TicketOrderReconciliationInfoDto CreateOrderInfo(
        int orderId,
        string status = "Paid",
        long finalAmount = PaymentAmount,
        int expectedTickets = 2,
        int issuedTickets = 1,
        int ticketsWithoutQr = 0)
    {
        return new TicketOrderReconciliationInfoDto
        {
            OrderId = orderId,
            EventId = 42,
            Status = status,
            FinalAmount = finalAmount,
            ExpectedTicketCount = expectedTickets,
            IssuedTicketCount = issuedTickets,
            TicketsWithoutQrCount = ticketsWithoutQr
        };
    }
}