using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TicketAPI.API;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;
using Order = TicketAPI.Models.Order;

namespace TicketAPI.Tests;

public class ViewOrderDetailsServiceTests
{
    private const int OrderId = 42;
    private const int OwnerCustomerId = 7;
    private const int OtherCustomerId = 8;

    [Fact]
    public async Task T01_OwnerCanRetrieveOrderDetails()
    {
        var order = CreateOrder();
        using var rig = CreateRig(order);

        var result = await rig.Service.GetOrderByOrderId(
            orderId: OrderId,
            customerId: OwnerCustomerId);

        Assert.Same(order, result);
        Assert.Equal(OrderId, result!.OrderId);
        Assert.Equal(OwnerCustomerId, result.CustomerId);
        Assert.Equal("Concert Shield Live", result.EventName);
        Assert.Equal("Paid", result.Status);
        Assert.Equal(2, result.OrderDetails.Count);
        Assert.Equal(2, result.OrderAttendees.Count);
        Assert.Single(result.Tickets);

        rig.Repository.Verify(
            repository => repository.GetOrderByOrderId(OrderId),
            Times.Once);
    }

    [Fact]
    public async Task T02_DifferentCustomerCannotRetrieveOrder()
    {
        var order = CreateOrder();
        using var rig = CreateRig(order);

        var result = await rig.Service.GetOrderByOrderId(
            orderId: OrderId,
            customerId: OtherCustomerId);

        Assert.Null(result);

        rig.Repository.Verify(
            repository => repository.GetOrderByOrderId(OrderId),
            Times.Once);
    }

    [Fact]
    public async Task T03_MissingOrderReturnsNullForCustomer()
    {
        using var rig = CreateRig(order: null);

        var result = await rig.Service.GetOrderByOrderId(
            orderId: OrderId,
            customerId: OwnerCustomerId);

        Assert.Null(result);

        rig.Repository.Verify(
            repository => repository.GetOrderByOrderId(OrderId),
            Times.Once);
    }

    [Fact]
    public async Task T04_PrivilegedLookupReturnsOrderWithoutCustomerFilter()
    {
        var order = CreateOrder(customerId: OtherCustomerId);
        using var rig = CreateRig(order);

        var result = await rig.Service.GetOrderByOrderId(OrderId);

        Assert.Same(order, result);
        Assert.Equal(OtherCustomerId, result!.CustomerId);

        rig.Repository.Verify(
            repository => repository.GetOrderByOrderId(OrderId),
            Times.Once);
    }

    [Fact]
    public async Task T05_PrivilegedLookupReturnsNullWhenOrderDoesNotExist()
    {
        using var rig = CreateRig(order: null);

        var result = await rig.Service.GetOrderByOrderId(OrderId);

        Assert.Null(result);

        rig.Repository.Verify(
            repository => repository.GetOrderByOrderId(OrderId),
            Times.Once);
    }

    [Fact]
    public async Task T06_RepositoryFailureIsPropagatedForCustomerLookup()
    {
        using var rig = CreateRig(order: null);

        rig.Repository
            .Setup(repository => repository.GetOrderByOrderId(OrderId))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.GetOrderByOrderId(
                orderId: OrderId,
                customerId: OwnerCustomerId));
    }

    [Fact]
    public async Task T07_RepositoryFailureIsPropagatedForPrivilegedLookup()
    {
        using var rig = CreateRig(order: null);

        rig.Repository
            .Setup(repository => repository.GetOrderByOrderId(OrderId))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.GetOrderByOrderId(OrderId));
    }

    private static Order CreateOrder(int customerId = OwnerCustomerId)
    {
        var order = new Order
        {
            OrderId = OrderId,
            CustomerId = customerId,
            EventId = 10,
            EventName = "Concert Shield Live",
            PosterUrl = "https://example.test/concert.jpg",
            FullName = "Test Customer",
            Email = "customer@example.test",
            Phone = "0900000000",
            OrderDate = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            StartsAt = new DateTime(2026, 11, 15, 19, 0, 0, DateTimeKind.Utc),
            TotalAmount = 400_000,
            DiscountAmount = 50_000,
            FinalAmount = 350_000,
            PaymentMethod = "VNPay",
            Status = "Paid",
            ExpiresAt = null
        };

        order.OrderDetails.Add(new OrderDetail
        {
            OrderDetailId = 1,
            OrderId = OrderId,
            TicketTypeId = 3,
            TicketTypeName = "Standard",
            Quantity = 1,
            UnitPrice = 200_000,
            SeatIds = "[101]",
            Order = order
        });

        order.OrderDetails.Add(new OrderDetail
        {
            OrderDetailId = 2,
            OrderId = OrderId,
            TicketTypeId = 4,
            TicketTypeName = "VIP",
            Quantity = 1,
            UnitPrice = 200_000,
            SeatIds = "[201]",
            Order = order
        });

        order.OrderAttendees.Add(new OrderAttendee
        {
            AttendeeId = 1,
            OrderId = OrderId,
            TicketTypeId = 3,
            FullName = "Attendee One",
            SeatId = 101,
            Order = order
        });

        order.OrderAttendees.Add(new OrderAttendee
        {
            AttendeeId = 2,
            OrderId = OrderId,
            TicketTypeId = 4,
            FullName = "Attendee Two",
            SeatId = 201,
            Order = order
        });

        order.Tickets.Add(new Ticket
        {
            TicketId = 301,
            TicketCode = Guid.NewGuid(),
            OrderId = OrderId,
            EventId = 10,
            TicketTypeId = 3,
            TicketTypeName = "Standard",
            OwnerUserId = customerId,
            OwnerName = "Attendee One",
            Status = "Active",
            Order = order
        });

        return order;
    }

    private static Rig CreateRig(Order? order)
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TicketDbContext(options);

        var repository = new Mock<IOrderRepository>();
        repository
            .Setup(item => item.GetOrderByOrderId(It.IsAny<int>()))
            .ReturnsAsync(order!);

        var service = new OrderService(
            context,
            Mock.Of<IEventApiClient>(),
            Mock.Of<IHoldService>(),
            Mock.Of<IHttpClientFactory>(),
            repository.Object,
            NullLogger<HoldService>.Instance,
            Mock.Of<IEmailService>(),
            Mock.Of<ITicketRepository>(),
            Mock.Of<IPaymentAPIClient>(),
            Mock.Of<IStatusHistoryService>());

        return new Rig(context, service, repository);
    }

    private sealed class Rig : IDisposable
    {
        public Rig(
            TicketDbContext context,
            OrderService service,
            Mock<IOrderRepository> repository)
        {
            Context = context;
            Service = service;
            Repository = repository;
        }

        public TicketDbContext Context { get; }
        public OrderService Service { get; }
        public Mock<IOrderRepository> Repository { get; }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}