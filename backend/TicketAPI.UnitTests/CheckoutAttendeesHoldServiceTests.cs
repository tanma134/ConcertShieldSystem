using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingAPI.DTOS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;
using Order = TicketAPI.Models.Order;

namespace TicketAPI.Tests;

public class CheckoutTests
{
    private const int EventId = 42;
    private const int CustomerId = 7;

    // Attendee form -> save attendee data into the hold session

    [Fact]
    public async Task C01_ValidSeatAttendeesAreSavedAndReturned()
    {
        var session = CreateHoldSession(
            seatIds: new List<int> { 101, 102 });

        var rig = CreateHoldRig(
            session: session,
            redisTtl: TimeSpan.FromMinutes(2));

        var result = await rig.Service.SaveAttendeesAsync(
            "hold-1", CustomerId, CreateAttendees(2));

        Assert.Equal("hold-1", result.HoldId);
        Assert.Equal(EventId, result.EventId);
        Assert.Equal(new[] { 101, 102 }, result.SeatIds);
        Assert.Equal(2, result.Attendees.Count);
        Assert.Equal("Attendee 1", result.Attendees[0].FullName);
        Assert.True(result.RemainingSeconds > 0);

        rig.Database.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(key =>
                key.ToString() == "hold:session:hold-1"),
            It.IsAny<RedisValue>(),
            TimeSpan.FromMinutes(2),
            When.Exists), Times.Once);

        rig.EligibilityHandler.VerifyRequestWasMade();
    }

    [Fact]
    public async Task C02_ValidGeneralAdmissionAttendeesAreSaved()
    {
        var session = CreateHoldSession(
            seatIds: new List<int>(),
            tickets: new List<TicketHoldItem>
            {
                new() { TicketTypeId = 3, Quantity = 2 }
            });

        var rig = CreateHoldRig(
            session: session,
            redisTtl: TimeSpan.FromMinutes(5));

        var result = await rig.Service.SaveAttendeesAsync(
            "hold-1", CustomerId, CreateAttendees(2));

        Assert.Equal(EventId, result.EventId);
        Assert.Empty(result.SeatIds);
        Assert.Equal(2, result.Attendees.Count);
        Assert.Equal("Attendee 1", result.Attendees[0].FullName);
        Assert.True(result.RemainingSeconds > 0);

        rig.Database.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(key =>
                key.ToString() == "hold:session:hold-1"),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            When.Exists), Times.Once);

        rig.EligibilityHandler.VerifyRequestWasMade();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task C03_EmptyHoldIdIsRejected(string holdId)
    {
        var rig = CreateHoldRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.SaveAttendeesAsync(
                holdId, CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotRead(rig.Database);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task C04_NonPositiveUserIdIsRejected(int userId)
    {
        var rig = CreateHoldRig();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", userId, CreateAttendees(1)));

        VerifyHoldSessionWasNotRead(rig.Database);
    }

    [Fact]
    public async Task C05_NullAttendeeListIsRejected()
    {
        var rig = CreateHoldRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.SaveAttendeesAsync("hold-1", CustomerId, null!));

        VerifyHoldSessionWasNotRead(rig.Database);
    }

    [Fact]
    public async Task C06_EmptyAttendeeListIsRejected()
    {
        var rig = CreateHoldRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1",
                CustomerId,
                new List<HoldAttendeeDto>()));

        VerifyHoldSessionWasNotRead(rig.Database);
    }

    [Fact]
    public async Task C07_MissingHoldSessionIsRejected()
    {
        var rig = CreateHoldRig(sessionExists: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C08_NullHoldSessionDataIsRejected()
    {
        var rig = CreateHoldRig(rawSessionJson: "null");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C09_MalformedHoldSessionJsonThrowsJsonException()
    {
        var rig = CreateHoldRig(rawSessionJson: "{invalid json");

        await Assert.ThrowsAsync<JsonException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C10_SessionOwnedByAnotherUserIsRejected()
    {
        var session = CreateHoldSession(userId: 99);
        var rig = CreateHoldRig(session: session);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C11_ExpiredSessionIsRejected()
    {
        var session = CreateHoldSession(
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var rig = CreateHoldRig(
            session: session,
            redisTtl: TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C12_MissingRedisTtlIsRejected()
    {
        var rig = CreateHoldRig(redisTtlIsMissing: true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C13_NonPositiveRedisTtlIsRejected()
    {
        var rig = CreateHoldRig(redisTtl: TimeSpan.Zero);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C14_AttendeeCountBelowExpectedCountIsRejected()
    {
        var session = CreateHoldSession(
            seatIds: new List<int> { 101, 102 });

        var rig = CreateHoldRig(session: session);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C15_AttendeeCountAboveExpectedCountIsRejected()
    {
        var session = CreateHoldSession(
            seatIds: new List<int> { 101, 102 });

        var rig = CreateHoldRig(session: session);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(3)));

        VerifyHoldSessionWasNotUpdated(rig.Database);
    }

    [Fact]
    public async Task C16_RedisRejectingSessionUpdateIsReportedAsExpiredSession()
    {
        var rig = CreateHoldRig(redisSaveResult: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        rig.EligibilityHandler.VerifyNoRequestWasMade();
    }

    [Fact]
    public async Task C17_ClosedSalesAreReportedAfterAttendeesAreSaved()
    {
        var rig = CreateHoldRig(canSell: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.SaveAttendeesAsync(
                "hold-1", CustomerId, CreateAttendees(1)));

        Assert.Equal("Sales are closed.", exception.Message);

        rig.Database.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(key =>
                key.ToString() == "hold:session:hold-1"),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            When.Exists), Times.Once);

        rig.EligibilityHandler.VerifyRequestWasMade();
    }

    // Payment Information -> send checkout data and create the order

    [Fact]
    public async Task O01_ValidReservedSeatCheckoutCreatesOrderWithAttendees()
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest(
            isReservedSeating: true,
            quantity: 2,
            seatIds: new List<int> { 101, 102 },
            attendeeCount: 2);

        var orderId = await rig.Service.Add(request, CustomerId);

        var order = Assert.Single(rig.Context.Orders.Local);
        var detail = Assert.Single(order.OrderDetails);

        Assert.True(orderId > 0);
        Assert.Equal(CustomerId, order.CustomerId);
        Assert.Equal(EventId, order.EventId);
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Main Event", order.EventName);
        Assert.Equal(400_000, order.TotalAmount);
        Assert.Equal(400_000, order.FinalAmount);
        Assert.Equal(0, order.DiscountAmount);

        Assert.Equal(3, detail.TicketTypeId);
        Assert.Equal(2, detail.Quantity);
        Assert.Equal(200_000, detail.UnitPrice);
        Assert.Equal(
            new[] { 101, 102 },
            JsonSerializer.Deserialize<List<int>>(detail.SeatIds!));

        Assert.Equal(2, order.OrderAttendees.Count);
        Assert.Equal("Attendee 1", order.OrderAttendees.First().FullName);
        Assert.Equal("ID1", order.OrderAttendees.First().CitizenId);
        Assert.Equal(101, order.OrderAttendees.First().SeatId);

        rig.StatusHistory.Verify(history => history.RecordAsync(
            It.Is<StatusHistory>(item =>
                item.Action == "OrderCreated" &&
                item.EventId == EventId &&
                item.ActorUserId == CustomerId)),
            Times.Once);
    }

    [Fact]
    public async Task O02_ValidGeneralAdmissionCheckoutCreatesOrder()
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest(
            isReservedSeating: false,
            quantity: 2,
            attendeeCount: 2);

        await rig.Service.Add(request, CustomerId);

        var order = Assert.Single(rig.Context.Orders.Local);
        var detail = Assert.Single(order.OrderDetails);

        Assert.Equal(400_000, order.TotalAmount);
        Assert.Equal(400_000, order.FinalAmount);
        Assert.Null(detail.SeatIds);
        Assert.Equal(2, order.OrderAttendees.Count);

        rig.HoldService.Verify(hold => hold.GetTicketHoldInfoAsync(
            3,
            CustomerId), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task O03_EmptyHoldIdIsRejected(string holdId)
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest();
        request.HoldId = holdId;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.Add(request, CustomerId));

        rig.HoldService.Verify(hold => hold.GetHoldDetailsAsync(
            It.IsAny<string>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task O04_HoldForAnotherEventIsRejected()
    {
        using var rig = CreateOrderRig();

        rig.HoldService
            .Setup(hold => hold.GetHoldDetailsAsync("hold-1", CustomerId))
            .ReturnsAsync(new HoldSessionResponse
            {
                HoldId = "hold-1",
                EventId = 999
            });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.Add(CreateOrderRequest(), CustomerId));

        rig.EventApi.Verify(api => api.GetEventByIdAsync(
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task O05_ReservedSeatingWithoutSeatIdsIsRejected()
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest(
            isReservedSeating: true,
            quantity: 1,
            seatIds: new List<int>());

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.EventApi.Verify(api => api.GetEventByIdAsync(
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task O06_SeatWithoutActiveHoldIsRejected()
    {
        using var rig = CreateOrderRig();

        rig.HoldService
            .Setup(hold => hold.GetHoldInfoAsync(EventId, 101))
            .ReturnsAsync((SeatHoldInfo?)null);

        var request = CreateOrderRequest(
            isReservedSeating: true,
            quantity: 1,
            seatIds: new List<int> { 101 });

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O07_SeatHeldByAnotherUserIsRejected()
    {
        using var rig = CreateOrderRig();

        rig.HoldService
            .Setup(hold => hold.GetHoldInfoAsync(EventId, 101))
            .ReturnsAsync(new SeatHoldInfo { UserId = 88 });

        var request = CreateOrderRequest(
            isReservedSeating: true,
            quantity: 1,
            seatIds: new List<int> { 101 });

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O08_GeneralAdmissionHoldWithInsufficientQuantityIsRejected()
    {
        using var rig = CreateOrderRig();

        rig.HoldService
            .Setup(hold => hold.GetTicketHoldInfoAsync(3, CustomerId))
            .ReturnsAsync(new TicketHoldInfo { Quantity = 1 });

        var request = CreateOrderRequest(
            isReservedSeating: false,
            quantity: 2,
            attendeeCount: 2);

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O09_EventNotFoundIsRejected()
    {
        using var rig = CreateOrderRig();

        rig.EventApi
            .Setup(api => api.GetEventByIdAsync(EventId))
            .ReturnsAsync((EventApiResponseDto?)null);

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(CreateOrderRequest(), CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O10_EventThatIsNotPublishedIsRejected()
    {
        using var rig = CreateOrderRig();

        rig.EventApi
            .Setup(api => api.GetEventByIdAsync(EventId))
            .ReturnsAsync(CreateEvent(status: "Draft"));

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(CreateOrderRequest(), CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O11_UnknownTicketTypeIsRejected()
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest();
        request.OrderDetails[0].TicketTypeId = 999;

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O12_QuantityBelowMinimumIsRejected()
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest(
            isReservedSeating: false,
            quantity: 0,
            attendeeCount: 0);

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O13_QuantityAboveMaximumIsRejected()
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest(
            isReservedSeating: false,
            quantity: 11,
            attendeeCount: 11);

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O14_ReservedSeatCountMustMatchTicketQuantity()
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest(
            isReservedSeating: true,
            quantity: 2,
            seatIds: new List<int> { 101 },
            attendeeCount: 2);

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O15_NonPositiveReservedSeatIdIsRejected()
    {
        using var rig = CreateOrderRig();

        var request = CreateOrderRequest(
            isReservedSeating: true,
            quantity: 1,
            seatIds: new List<int> { 0 });

        await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O16_InvalidVoucherIsRejected()
    {
        using var rig = CreateOrderRig();

        rig.PaymentApi
            .Setup(payment => payment.ApplyVoucherAsync(
                It.IsAny<ApplyVoucherRequestDto>()))
            .ReturnsAsync(new ApplyVoucherResponseDto
            {
                IsValid = false,
                Message = "Voucher is invalid."
            });

        var request = CreateOrderRequest(voucherCode: " BADCODE ");

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(request, CustomerId));

        Assert.Contains("Voucher is invalid.", exception.Message);

        rig.OrderRepository.Verify(repository =>
            repository.AddCompleteOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task O17_ValidVoucherUpdatesOrderAmounts()
    {
        using var rig = CreateOrderRig();

        rig.PaymentApi
            .Setup(payment => payment.ApplyVoucherAsync(
                It.IsAny<ApplyVoucherRequestDto>()))
            .ReturnsAsync(new ApplyVoucherResponseDto
            {
                IsValid = true,
                DiscountAmount = 50_000,
                FinalAmount = 350_000,
                VoucherId = 12
            });

        var request = CreateOrderRequest(
            isReservedSeating: false,
            quantity: 2,
            attendeeCount: 2,
            voucherCode: " SAVE50 ");

        await rig.Service.Add(request, CustomerId);

        var order = Assert.Single(rig.Context.Orders.Local);

        Assert.Equal(400_000, order.TotalAmount);
        Assert.Equal(50_000, order.DiscountAmount);
        Assert.Equal(350_000, order.FinalAmount);
        Assert.Equal(12, order.VoucherId);

        rig.PaymentApi.Verify(payment => payment.ApplyVoucherAsync(
            It.Is<ApplyVoucherRequestDto>(voucher =>
                voucher.Code == "SAVE50" &&
                voucher.EventId == EventId &&
                voucher.OrderAmount == 400_000)), Times.Once);
    }

    [Fact]
    public async Task O18_OrderRepositoryFailureIsReported()
    {
        using var rig = CreateOrderRig();

        rig.OrderRepository
            .Setup(repository =>
                repository.AddCompleteOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            rig.Service.Add(CreateOrderRequest(), CustomerId));

        Assert.Contains("Database unavailable", exception.Message);

        rig.StatusHistory.Verify(
            history => history.RecordAsync(It.IsAny<StatusHistory>()),
            Times.Never);
    }

    private static HoldRig CreateHoldRig(
        HoldSession? session = null,
        string? rawSessionJson = null,
        bool sessionExists = true,
        TimeSpan? redisTtl = null,
        bool redisTtlIsMissing = false,
        bool redisSaveResult = true,
        bool canSell = true)
    {
        session ??= CreateHoldSession();

        var json = rawSessionJson ??
            JsonSerializer.Serialize(
                session,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true
                });

        var redisValue = sessionExists
            ? (RedisValue)json
            : RedisValue.Null;

        var database = new Mock<IDatabase>();

        database
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisValue);

        TimeSpan? ttlValue = redisTtlIsMissing
            ? null
            : redisTtl ?? TimeSpan.FromMinutes(2);

        database
            .Setup(db => db.KeyTimeToLiveAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(ttlValue);

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                When.Exists))
            .ReturnsAsync(true);

        if (!redisSaveResult)
        {
            database
                .Setup(db => db.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.Exists))
                .ReturnsAsync(false);
        }

        var redis = new Mock<IConnectionMultiplexer>();

        redis
            .Setup(connection => connection.GetDatabase(
                It.IsAny<int>(),
                It.IsAny<object?>()))
            .Returns(database.Object);

        var eligibilityHandler = new EligibilityHandler(canSell);
        var eligibilityClient = new EventEligibilityClient(
            new HttpClient(eligibilityHandler)
            {
                BaseAddress = new Uri("https://test.local/")
            });

        var service = new HoldService(
            redis.Object,
            NullLogger<HoldService>.Instance,
            Mock.Of<IEventApiClient>(),
            eligibilityClient);

        return new HoldRig(service, database, eligibilityHandler);
    }

    private static OrderRig CreateOrderRig()
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new TicketDbContext(options);

        var holdService = new Mock<IHoldService>();

        holdService
            .Setup(hold => hold.GetHoldDetailsAsync("hold-1", CustomerId))
            .ReturnsAsync(new HoldSessionResponse
            {
                HoldId = "hold-1",
                EventId = EventId,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            });

        holdService
            .Setup(hold => hold.GetHoldInfoAsync(
                EventId,
                It.IsAny<int>()))
            .ReturnsAsync(new SeatHoldInfo { UserId = CustomerId });

        holdService
            .Setup(hold => hold.GetTicketHoldInfoAsync(3, CustomerId))
            .ReturnsAsync(new TicketHoldInfo { Quantity = 99 });

        var eventApi = new Mock<IEventApiClient>();

        eventApi
            .Setup(api => api.GetEventByIdAsync(EventId))
            .ReturnsAsync(CreateEvent());

        var orderRepository = new Mock<IOrderRepository>();

        orderRepository
            .Setup(repository =>
                repository.AddCompleteOrderAsync(It.IsAny<Order>()))
            .Callback<Order>(order => context.Orders.Add(order))
            .Returns(Task.CompletedTask);

        var paymentApi = new Mock<IPaymentAPIClient>();

        var statusHistory = new Mock<IStatusHistoryService>();

        statusHistory
            .Setup(history => history.RecordAsync(It.IsAny<StatusHistory>()))
            .Returns(Task.CompletedTask);

        var service = new OrderService(
            context,
            eventApi.Object,
            holdService.Object,
            Mock.Of<IHttpClientFactory>(),
            orderRepository.Object,
            NullLogger<HoldService>.Instance,
            Mock.Of<IEmailService>(),
            Mock.Of<ITicketRepository>(),
            paymentApi.Object,
            statusHistory.Object);

        return new OrderRig(
            context,
            service,
            holdService,
            eventApi,
            orderRepository,
            paymentApi,
            statusHistory);
    }

    private static HoldSession CreateHoldSession(
        int userId = CustomerId,
        List<int>? seatIds = null,
        List<TicketHoldItem>? tickets = null,
        DateTime? expiresAtUtc = null)
    {
        return new HoldSession
        {
            HoldId = "hold-1",
            UserId = userId,
            EventId = EventId,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            DurationSeconds = 600,
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(5),
            SeatIds = seatIds ?? new List<int> { 101 },
            Tickets = tickets ?? new List<TicketHoldItem>(),
            Attendees = new List<HoldAttendeeDto>()
        };
    }

    private static List<HoldAttendeeDto> CreateAttendees(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new HoldAttendeeDto
            {
                FullName = $"Attendee {index}",
                CitizenId = $"ID{index}",
                Phone = $"090000000{index}",
                Email = index == 1 ? "buyer@example.com" : null,
                TicketTypeId = 3,
                SeatId = index == 1 ? 101 : 102,
                IsPrimaryBuyer = index == 1
            })
            .ToList();
    }

    private static CreateOrderDTO CreateOrderRequest(
        bool isReservedSeating = false,
        int quantity = 1,
        List<int>? seatIds = null,
        int attendeeCount = 1,
        string? voucherCode = null)
    {
        return new CreateOrderDTO
        {
            EventId = EventId,
            HoldId = "hold-1",
            IsReservedSeating = isReservedSeating,
            FullName = "Buyer One",
            Email = "buyer@example.com",
            Phone = "0900000001",
            VoucherCode = voucherCode,
            OrderDetails = new List<CreateOrderDetailsDTO>
            {
                new()
                {
                    TicketTypeId = 3,
                    Quantity = quantity,
                    SeatIds = seatIds ?? new List<int>()
                }
            },
            Attendees = Enumerable.Range(1, attendeeCount)
                .Select(index => new HoldAttendeeDto
                {
                    FullName = $"Attendee {index}",
                    CitizenId = $"ID{index}",
                    Phone = $"090000000{index}",
                    Email = index == 1 ? "buyer@example.com" : null,
                    TicketTypeId = 3,
                    SeatId = seatIds != null && index <= seatIds.Count
                        ? seatIds[index - 1]
                        : null,
                    IsPrimaryBuyer = index == 1
                })
                .ToList()
        };
    }

    private static EventApiResponseDto CreateEvent(string status = "Published")
    {
        return new EventApiResponseDto
        {
            EventId = EventId,
            Title = "Main Event",
            Status = status,
            StartsAt = DateTime.UtcNow.AddDays(7),
            EndsAt = DateTime.UtcNow.AddDays(7).AddHours(3),
            TicketTypes = new List<TicketTypeApiResponseDto>
            {
                new()
                {
                    TicketTypeId = 3,
                    TypeName = "Standard",
                    Price = 200_000,
                    Quantity = 100,
                    SoldQuantity = 0,
                    Status = "Active",
                    MinPerOrder = 1,
                    MaxPerOrder = 10
                }
            }
        };
    }

    private static void VerifyHoldSessionWasNotRead(
        Mock<IDatabase> database)
    {
        database.Verify(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    private static void VerifyHoldSessionWasNotUpdated(
        Mock<IDatabase> database)
    {
        database.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            When.Exists), Times.Never);
    }

    private sealed class HoldRig
    {
        public HoldRig(
            HoldService service,
            Mock<IDatabase> database,
            EligibilityHandler eligibilityHandler)
        {
            Service = service;
            Database = database;
            EligibilityHandler = eligibilityHandler;
        }

        public HoldService Service { get; }
        public Mock<IDatabase> Database { get; }
        public EligibilityHandler EligibilityHandler { get; }
    }

    private sealed class OrderRig : IDisposable
    {
        public OrderRig(
            TicketDbContext context,
            OrderService service,
            Mock<IHoldService> holdService,
            Mock<IEventApiClient> eventApi,
            Mock<IOrderRepository> orderRepository,
            Mock<IPaymentAPIClient> paymentApi,
            Mock<IStatusHistoryService> statusHistory)
        {
            Context = context;
            Service = service;
            HoldService = holdService;
            EventApi = eventApi;
            OrderRepository = orderRepository;
            PaymentApi = paymentApi;
            StatusHistory = statusHistory;
        }

        public TicketDbContext Context { get; }
        public OrderService Service { get; }
        public Mock<IHoldService> HoldService { get; }
        public Mock<IEventApiClient> EventApi { get; }
        public Mock<IOrderRepository> OrderRepository { get; }
        public Mock<IPaymentAPIClient> PaymentApi { get; }
        public Mock<IStatusHistoryService> StatusHistory { get; }

        public void Dispose()
        {
            Context.Dispose();
        }
    }

    private sealed class EligibilityHandler : HttpMessageHandler
    {
        private readonly bool _canSell;
        private int _requestCount;

        public EligibilityHandler(bool canSell)
        {
            _canSell = canSell;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            var result = new EventEligibility(
                CanSell: _canSell,
                Reason: _canSell ? string.Empty : "Sales are closed.",
                Status: _canSell ? "Published" : "Closed",
                StartsAt: DateTime.UtcNow.AddDays(-1),
                EndsAt: DateTime.UtcNow.AddDays(1),
                ScheduleVersion: 1,
                RequiresVirtualQueue: false);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(result)
            });
        }

        public void VerifyRequestWasMade()
        {
            Assert.Equal(1, _requestCount);
        }

        public void VerifyNoRequestWasMade()
        {
            Assert.Equal(0, _requestCount);
        }
    }
}