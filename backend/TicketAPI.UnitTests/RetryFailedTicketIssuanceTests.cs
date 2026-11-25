using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TicketAPI.API;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;
using Order = TicketAPI.Models.Order;

namespace TicketAPI.Tests;

public class RetryFailedTicketIssuanceTests
{
    private const int OrderId = 42;
    private const int CustomerId = 7;
    private const int EventId = 10;
    private const int TicketTypeId = 3;
    private const long Amount = 200_000;
    private const string TransactionRef = "vnp-reconciliation-ref";

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task R01_NonPositiveOrderIdIsRejected(int orderId)
    {
        using var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                orderId, Amount, TransactionRef));

        rig.OrderRepository.Verify(
            repository => repository.GetOrderByOrderId(It.IsAny<int>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task R02_NonPositivePaymentAmountIsRejected(long amount)
    {
        using var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, amount, TransactionRef));

        VerifyOrderWasNotLoaded(rig);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task R03_MissingTransactionReferenceIsRejected(
        string? transactionRef)
    {
        using var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, transactionRef!));

        VerifyOrderWasNotLoaded(rig);
    }

    [Fact]
    public async Task R04_OrderDoesNotExist_ThrowsKeyNotFoundException()
    {
        using var rig = CreateRig(order: null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, TransactionRef));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R05_DeletedOrder_ThrowsKeyNotFoundException()
    {
        var order = CreateOrder();
        order.IsDeleted = true;
        using var rig = CreateRig(order);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, TransactionRef));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R06_OrderIsNotPaid_IsRejected()
    {
        var order = CreateOrder();
        order.Status = "Pending";
        using var rig = CreateRig(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, TransactionRef));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R07_PaymentAmountDoesNotMatchOrderAmount_IsRejected()
    {
        // R07
        using var rig = CreateRig(CreateOrder());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount + 1, TransactionRef));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R08_TransactionReferenceDoesNotMatchOrder_IsRejected()
    {
        // R08
        using var rig = CreateRig(CreateOrder());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, "different-reference"));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R09_AttendeeCountDoesNotMatchExpectedTicketCount_IsRejected()
    {
        var order = CreateOrder(attendeeCount: 1);
        using var rig = CreateRig(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, TransactionRef));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R10_ExistingTicketsExceedExpectedCount_IsRejected()
    {
        var order = CreateOrder(existingTicketCount: 3);
        using var rig = CreateRig(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, TransactionRef));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R11_OnlySomeTicketsExist_IsRejectedToAvoidIncorrectReissue()
    {
        var order = CreateOrder(existingTicketCount: 1);
        using var rig = CreateRig(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, TransactionRef));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R12_SeatIdsDoNotMatchAttendees_IsRejected()
    {
        var order = CreateOrder(
            seatIds: new[] { 101, 102 },
            attendeeSeatIds: new int?[] { 101, 999 });
        using var rig = CreateRig(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RepairPaidOrderArtifactsAsync(
                OrderId, Amount, TransactionRef));

        VerifyNoTicketsOrQrWereAdded(rig);
    }

    [Fact]
    public async Task R13_NoTicketsExist_CreatesTicketsQrTokensAndHistory()
    {
        var order = CreateOrder();
        using var rig = CreateRig(order);
        var before = DateTime.UtcNow;

        await rig.Service.RepairPaidOrderArtifactsAsync(
            OrderId, Amount, TransactionRef);

        var after = DateTime.UtcNow;

        Assert.Equal(2, rig.CreatedTickets.Count);
        Assert.Equal(2, rig.CreatedQrTokens.Count);

        Assert.All(rig.CreatedTickets, ticket =>
        {
            Assert.Equal(OrderId, ticket.OrderId);
            Assert.Equal(EventId, ticket.EventId);
            Assert.Equal(TicketTypeId, ticket.TicketTypeId);
            Assert.Equal(CustomerId, ticket.OwnerUserId);
            Assert.Equal("Active", ticket.Status);
            Assert.False(ticket.IsDeleted);
            Assert.NotEqual(Guid.Empty, ticket.TicketCode);
        });

        Assert.Contains(
            rig.CreatedTickets,
            ticket => ticket.OwnerName == "Attendee 1");

        Assert.Contains(
            rig.CreatedTickets,
            ticket => ticket.OwnerName == "Attendee 2");

        Assert.Equal(
            rig.CreatedTickets.Select(ticket => ticket.TicketId).OrderBy(id => id),
            rig.CreatedQrTokens.Select(token => token.TicketId).OrderBy(id => id));

        Assert.All(rig.CreatedQrTokens, token =>
        {
            Assert.False(string.IsNullOrWhiteSpace(token.QrToken));
            Assert.False(token.IsRevoked);
            Assert.False(token.IsUsed);
            Assert.InRange(token.IssuedAt, before, after);
            Assert.Equal(
                token.IssuedAt.AddSeconds(60),
                token.ExpiresAt);
        });

        Assert.Equal(2, rig.RecordedHistory.Count);
        Assert.All(rig.RecordedHistory, history =>
        {
            Assert.Equal("Ticket", history.TargetType);
            Assert.Equal("TicketIssued", history.Action);
            Assert.Equal("Active", history.NewStatus);
            Assert.Equal("System", history.ActorRole);
            Assert.Equal(
                "{\"source\":\"PaymentReconciliationRepair\"}",
                history.Metadata);
        });

        rig.TicketRepository.Verify(
            repository => repository.AddRangeAsync(
                It.IsAny<IEnumerable<Ticket>>()),
            Times.Once);

        rig.TicketRepository.Verify(
            repository => repository.AddQrTokensAsync(
                It.IsAny<IEnumerable<TicketQrToken>>()),
            Times.Once);

        rig.StatusHistory.Verify(
            service => service.RecordRangeAsync(
                It.IsAny<IEnumerable<StatusHistory>>()),
            Times.Once);
    }

    [Fact]
    public async Task R14_AllTicketsExistButSomeQrTokensAreMissing_AddsOnlyMissingQrTokens()
    {
        var order = CreateOrder(
            existingTicketCount: 2,
            ticketWithQrIds: new[] { 501 });
        using var rig = CreateRig(order);

        await rig.Service.RepairPaidOrderArtifactsAsync(
            OrderId, Amount, TransactionRef);

        Assert.Empty(rig.CreatedTickets);

        var token = Assert.Single(rig.CreatedQrTokens);
        Assert.Equal(502, token.TicketId);
        Assert.False(string.IsNullOrWhiteSpace(token.QrToken));
        Assert.False(token.IsRevoked);
        Assert.False(token.IsUsed);

        rig.TicketRepository.Verify(
            repository => repository.AddRangeAsync(
                It.IsAny<IEnumerable<Ticket>>()),
            Times.Never);

        rig.StatusHistory.Verify(
            service => service.RecordRangeAsync(
                It.IsAny<IEnumerable<StatusHistory>>()),
            Times.Never);
    }

    [Fact]
    public async Task R15_AllTicketsAndQrTokensAlreadyExist_DoesNotCreateDuplicates()
    {
        var order = CreateOrder(
            existingTicketCount: 2,
            ticketWithQrIds: new[] { 501, 502 });
        using var rig = CreateRig(order);

        await rig.Service.RepairPaidOrderArtifactsAsync(
            OrderId, Amount, TransactionRef);

        Assert.Empty(rig.CreatedTickets);
        Assert.Empty(rig.CreatedQrTokens);
        Assert.Empty(rig.RecordedHistory);

        rig.TicketRepository.Verify(
            repository => repository.AddRangeAsync(
                It.IsAny<IEnumerable<Ticket>>()),
            Times.Never);

        rig.TicketRepository.Verify(
            repository => repository.AddQrTokensAsync(
                It.IsAny<IEnumerable<TicketQrToken>>()),
            Times.Never);

        rig.StatusHistory.Verify(
            service => service.RecordRangeAsync(
                It.IsAny<IEnumerable<StatusHistory>>()),
            Times.Never);
    }

    private static Rig CreateRig(Order? order = null)
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new TicketDbContext(options);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(repository => repository.GetOrderByOrderId(OrderId))
            .ReturnsAsync(order!);

        var ticketRepository = new Mock<ITicketRepository>();
        var createdTickets = new List<Ticket>();
        var createdQrTokens = new List<TicketQrToken>();
        var recordedHistory = new List<StatusHistory>();
        var nextTicketId = 900;

        ticketRepository
            .Setup(repository => repository.AddRangeAsync(
                It.IsAny<IEnumerable<Ticket>>()))
            .Callback<IEnumerable<Ticket>>(tickets =>
            {
                foreach (var ticket in tickets)
                {
                    ticket.TicketId = nextTicketId++;
                    createdTickets.Add(ticket);
                }
            })
            .Returns(Task.CompletedTask);

        ticketRepository
            .Setup(repository => repository.AddQrTokensAsync(
                It.IsAny<IEnumerable<TicketQrToken>>()))
            .Callback<IEnumerable<TicketQrToken>>(tokens =>
                createdQrTokens.AddRange(tokens))
            .Returns(Task.CompletedTask);

        var statusHistory = new Mock<IStatusHistoryService>();
        statusHistory
            .Setup(service => service.RecordRangeAsync(
                It.IsAny<IEnumerable<StatusHistory>>()))
            .Callback<IEnumerable<StatusHistory>>(histories =>
                recordedHistory.AddRange(histories))
            .Returns(Task.CompletedTask);

        var service = new OrderService(
            context,
            Mock.Of<IEventApiClient>(),
            Mock.Of<IHoldService>(),
            Mock.Of<IHttpClientFactory>(),
            orderRepository.Object,
            NullLogger<HoldService>.Instance,
            Mock.Of<IEmailService>(),
            ticketRepository.Object,
            Mock.Of<IPaymentAPIClient>(),
            statusHistory.Object);

        return new Rig(
            context,
            service,
            orderRepository,
            ticketRepository,
            statusHistory,
            createdTickets,
            createdQrTokens,
            recordedHistory);
    }

    private static Order CreateOrder(
        int attendeeCount = 2,
        int existingTicketCount = 0,
        int[]? seatIds = null,
        int?[]? attendeeSeatIds = null,
        int[]? ticketWithQrIds = null)
    {
        seatIds ??= Array.Empty<int>();
        attendeeSeatIds ??= new int?[] { null, null };
        ticketWithQrIds ??= Array.Empty<int>();

        var order = new Order
        {
            OrderId = OrderId,
            CustomerId = CustomerId,
            EventId = EventId,
            Status = "Paid",
            FinalAmount = Amount,
            PaymentGatewayRef = TransactionRef
        };

        order.OrderDetails.Add(new OrderDetail
        {
            OrderDetailId = 1,
            OrderId = OrderId,
            TicketTypeId = TicketTypeId,
            TicketTypeName = "Standard",
            Quantity = 2,
            UnitPrice = Amount / 2,
            SeatIds = seatIds.Length == 0
                ? null
                : System.Text.Json.JsonSerializer.Serialize(seatIds),
            Order = order
        });

        for (var index = 0; index < attendeeCount; index++)
        {
            order.OrderAttendees.Add(new OrderAttendee
            {
                AttendeeId = index + 1,
                OrderId = OrderId,
                TicketTypeId = TicketTypeId,
                FullName = $"Attendee {index + 1}",
                SeatId = index < attendeeSeatIds.Length
                    ? attendeeSeatIds[index]
                    : null,
                Order = order
            });
        }

        for (var index = 0; index < existingTicketCount; index++)
        {
            var ticketId = 501 + index;
            var ticket = new Ticket
            {
                TicketId = ticketId,
                TicketCode = Guid.NewGuid(),
                OrderId = OrderId,
                EventId = EventId,
                TicketTypeId = TicketTypeId,
                TicketTypeName = "Standard",
                OwnerUserId = CustomerId,
                OwnerName = $"Attendee {index + 1}",
                Status = "Active",
                IsDeleted = false
            };

            if (ticketWithQrIds.Contains(ticketId))
            {
                ticket.TicketQrTokens.Add(new TicketQrToken
                {
                    TicketQrTokenId = 700 + index,
                    TicketId = ticketId,
                    QrToken = $"existing-qr-{ticketId}",
                    IssuedAt = DateTime.UtcNow.AddMinutes(-1),
                    ExpiresAt = DateTime.UtcNow.AddSeconds(59),
                    IsRevoked = false,
                    IsUsed = false,
                    Ticket = ticket
                });
            }

            order.Tickets.Add(ticket);
        }

        return order;
    }

    private static void VerifyOrderWasNotLoaded(Rig rig)
    {
        rig.OrderRepository.Verify(
            repository => repository.GetOrderByOrderId(It.IsAny<int>()),
            Times.Never);
    }

    private static void VerifyNoTicketsOrQrWereAdded(Rig rig)
    {
        Assert.Empty(rig.CreatedTickets);
        Assert.Empty(rig.CreatedQrTokens);
        Assert.Empty(rig.RecordedHistory);
    }

    private sealed class Rig : IDisposable
    {
        public Rig(
            TicketDbContext context,
            OrderService service,
            Mock<IOrderRepository> orderRepository,
            Mock<ITicketRepository> ticketRepository,
            Mock<IStatusHistoryService> statusHistory,
            List<Ticket> createdTickets,
            List<TicketQrToken> createdQrTokens,
            List<StatusHistory> recordedHistory)
        {
            Context = context;
            Service = service;
            OrderRepository = orderRepository;
            TicketRepository = ticketRepository;
            StatusHistory = statusHistory;
            CreatedTickets = createdTickets;
            CreatedQrTokens = createdQrTokens;
            RecordedHistory = recordedHistory;
        }

        public TicketDbContext Context { get; }
        public OrderService Service { get; }
        public Mock<IOrderRepository> OrderRepository { get; }
        public Mock<ITicketRepository> TicketRepository { get; }
        public Mock<IStatusHistoryService> StatusHistory { get; }
        public List<Ticket> CreatedTickets { get; }
        public List<TicketQrToken> CreatedQrTokens { get; }
        public List<StatusHistory> RecordedHistory { get; }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}