using BookingAPI.DTOS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.Tests;

public class ConcertOrderListServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task T01_NonPositiveEventIdIsRejected(int eventId)
    {
        using var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.GetOrganizerEventOrdersAsync(
                eventId,
                "organizer-token",
                new OrganizerOrderQueryDto()));

        rig.EventApi.Verify(
            api => api.GetOwnedEventAsync(
                It.IsAny<int>(),
                It.IsAny<string?>()),
            Times.Never);

        rig.OrderRepository.Verify(
            repository => repository.GetEventOrdersAsync(
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task T02_FromDateLaterThanToDateIsRejected()
    {
        using var rig = CreateRig();

        var query = new OrganizerOrderQueryDto
        {
            FromDate = new DateOnly(2026, 10, 10),
            ToDate = new DateOnly(2026, 10, 9)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.GetOrganizerEventOrdersAsync(
                42,
                "organizer-token",
                query));

        rig.EventApi.Verify(
            api => api.GetOwnedEventAsync(
                It.IsAny<int>(),
                It.IsAny<string?>()),
            Times.Never);

        rig.OrderRepository.Verify(
            repository => repository.GetEventOrdersAsync(
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task T03_EventNotFoundThrowsKeyNotFoundException()
    {
        using var rig = CreateRig(eventFound: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.GetOrganizerEventOrdersAsync(
                42,
                "organizer-token",
                new OrganizerOrderQueryDto()));

        rig.OrderRepository.Verify(
            repository => repository.GetEventOrdersAsync(
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task T04_PageBelowOneIsNormalizedToOne(int requestedPage)
    {
        using var rig = CreateRig();

        var result = await rig.Service.GetOrganizerEventOrdersAsync(
            42,
            "organizer-token",
            new OrganizerOrderQueryDto
            {
                Page = requestedPage,
                PageSize = 10
            });

        Assert.Equal(1, result.Page);

        rig.OrderRepository.Verify(
            repository => repository.GetEventOrdersAsync(
                42,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                1,
                10),
            Times.Once);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(101, 100)]
    public async Task T05_PageSizeIsClampedToAllowedRange(
        int requestedPageSize,
        int expectedPageSize)
    {
        using var rig = CreateRig();

        var result = await rig.Service.GetOrganizerEventOrdersAsync(
            42,
            "organizer-token",
            new OrganizerOrderQueryDto
            {
                Page = 2,
                PageSize = requestedPageSize
            });

        Assert.Equal(2, result.Page);
        Assert.Equal(expectedPageSize, result.PageSize);

        rig.OrderRepository.Verify(
            repository => repository.GetEventOrdersAsync(
                42,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                2,
                expectedPageSize),
            Times.Once);
    }

    [Fact]
    public async Task T06_ValidFiltersAreForwardedAndOrdersAreMapped()
    {
        var firstOrder = CreateOrder(
            orderId: 501,
            eventName: null,
            quantities: new[] { 2, 3 },
            discountAmount: null);

        var secondOrder = CreateOrder(
            orderId: 502,
            eventName: "Custom Event Name",
            quantities: new[] { 1 },
            discountAmount: 100);

        using var rig = CreateRig(
            orders: new[] { firstOrder, secondOrder },
            totalCount: 3);

        var query = new OrganizerOrderQueryDto
        {
            Search = "alex@example.com",
            Status = "Paid",
            FromDate = new DateOnly(2026, 10, 1),
            ToDate = new DateOnly(2026, 10, 3),
            Page = 2,
            PageSize = 2
        };

        var result = await rig.Service.GetOrganizerEventOrdersAsync(
            42,
            "Bearer organizer-token",
            query);

        Assert.Equal(42, result.EventId);
        Assert.Equal("Concert Night", result.EventName);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);

        var first = result.Items[0];
        Assert.Equal(501, first.OrderId);
        Assert.Equal(42, first.EventId);
        Assert.Equal("Concert Night", first.EventName);
        Assert.Equal(7, first.CustomerId);
        Assert.Equal("Alex Nguyen", first.CustomerName);
        Assert.Equal("alex@example.com", first.CustomerEmail);
        Assert.Equal(5, first.TicketCount);
        Assert.Equal(1000000, first.TotalAmount);
        Assert.Equal(0, first.DiscountAmount);
        Assert.Equal(1000000, first.FinalAmount);
        Assert.Equal("VNPay", first.PaymentMethod);
        Assert.Equal("Paid", first.Status);

        var second = result.Items[1];
        Assert.Equal("Custom Event Name", second.EventName);
        Assert.Equal(1, second.TicketCount);
        Assert.Equal(100, second.DiscountAmount);

        rig.EventApi.Verify(
            api => api.GetOwnedEventAsync(
                42,
                "Bearer organizer-token"),
            Times.Once);

        rig.OrderRepository.Verify(
            repository => repository.GetEventOrdersAsync(
                42,
                "alex@example.com",
                "Paid",
                It.Is<DateTime?>(date =>
                    date.HasValue &&
                    date.Value == new DateTime(2026, 10, 1)),
                It.Is<DateTime?>(date =>
                    date.HasValue &&
                    date.Value == new DateTime(2026, 10, 4)),
                2,
                2),
            Times.Once);
    }

    [Fact]
    public async Task T07_EmptyRepositoryResultReturnsEmptyOrderList()
    {
        using var rig = CreateRig();

        var result = await rig.Service.GetOrganizerEventOrdersAsync(
            42,
            "organizer-token",
            new OrganizerOrderQueryDto
            {
                Page = 1,
                PageSize = 10
            });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task T08_OrderRepositoryFailureIsPropagated()
    {
        using var rig = CreateRig();

        rig.OrderRepository
            .Setup(repository => repository.GetEventOrdersAsync(
                42,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.GetOrganizerEventOrdersAsync(
                42,
                "organizer-token",
                new OrganizerOrderQueryDto()));

        rig.OrderRepository.Verify(
            repository => repository.GetEventOrdersAsync(
                42,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                1,
                10),
            Times.Once);
    }

    [Fact]
    public async Task T09_EventApiFailureIsPropagated()
    {
        using var rig = CreateRig();

        rig.EventApi
            .Setup(api => api.GetOwnedEventAsync(
                42,
                "organizer-token"))
            .ThrowsAsync(new HttpRequestException(
                "Event service unavailable"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            rig.Service.GetOrganizerEventOrdersAsync(
                42,
                "organizer-token",
                new OrganizerOrderQueryDto()));

        rig.OrderRepository.Verify(
            repository => repository.GetEventOrdersAsync(
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Never);
    }

    private static Order CreateOrder(
        int orderId,
        string? eventName,
        int[] quantities,
        long? discountAmount)
    {
        return new Order
        {
            OrderId = orderId,
            EventId = 42,
            CustomerId = 7,
            FullName = "Alex Nguyen",
            Email = "alex@example.com",
            EventName = eventName,
            OrderDate = new DateTime(2026, 10, 2, 12, 0, 0),
            TotalAmount = 1000000,
            DiscountAmount = discountAmount,
            FinalAmount = 1000000,
            PaymentMethod = "VNPay",
            Status = "Paid",
            OrderDetails = quantities
                .Select((quantity, index) => new OrderDetail
                {
                    OrderId = orderId,
                    TicketTypeId = index + 1,
                    Quantity = quantity
                })
                .ToList()
        };
    }

    private static Rig CreateRig(
        IEnumerable<Order>? orders = null,
        int totalCount = 0,
        bool eventFound = true)
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TicketDbContext(options);

        var eventApi = new Mock<IEventApiClient>();
        eventApi
            .Setup(api => api.GetOwnedEventAsync(
                It.IsAny<int>(),
                It.IsAny<string?>()))
            .ReturnsAsync(eventFound
                ? new EventApiResponseDto
                {
                    EventId = 42,
                    Title = "Concert Night"
                }
                : null);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(repository => repository.GetEventOrdersAsync(
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((
                orders?.ToList() ?? new List<Order>(),
                totalCount));

        var service = new OrderService(
            context,
            eventApi.Object,
            Mock.Of<IHoldService>(),
            Mock.Of<IHttpClientFactory>(),
            orderRepository.Object,
            NullLogger<HoldService>.Instance,
            Mock.Of<IEmailService>(),
            Mock.Of<ITicketRepository>(),
            Mock.Of<IPaymentAPIClient>(),
            Mock.Of<IStatusHistoryService>());

        return new Rig(
            context,
            service,
            eventApi,
            orderRepository);
    }

    private sealed class Rig : IDisposable
    {
        public Rig(
            TicketDbContext context,
            OrderService service,
            Mock<IEventApiClient> eventApi,
            Mock<IOrderRepository> orderRepository)
        {
            Context = context;
            Service = service;
            EventApi = eventApi;
            OrderRepository = orderRepository;
        }

        public TicketDbContext Context { get; }
        public OrderService Service { get; }
        public Mock<IEventApiClient> EventApi { get; }
        public Mock<IOrderRepository> OrderRepository { get; }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}