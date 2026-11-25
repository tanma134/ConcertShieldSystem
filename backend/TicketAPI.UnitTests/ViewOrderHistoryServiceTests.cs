using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;
using Order = TicketAPI.Models.Order;

namespace TicketAPI.Tests;

public class ViewOrderHistoryServiceTests
{
    [Fact]
    public async Task T01_MapsOrderFieldsAndSumsTicketQuantities()
    {
        var orderDate = new DateTime(2026, 10, 1, 10, 30, 0, DateTimeKind.Utc);
        var startsAt = new DateTime(2026, 11, 15, 19, 0, 0, DateTimeKind.Utc);
        var expiresAt = new DateTime(2026, 10, 1, 10, 45, 0, DateTimeKind.Utc);

        var order = CreateOrder(
            orderId: 101,
            eventId: 42,
            eventName: "Concert A",
            posterUrl: "https://example.test/poster.jpg",
            orderDate: orderDate,
            startsAt: startsAt,
            totalAmount: 500_000,
            discountAmount: 50_000,
            finalAmount: 450_000,
            paymentMethod: "VNPay",
            status: "Paid",
            expiresAt: expiresAt,
            quantities: new[] { 2, 3 });

        using var rig = CreateRig(new[] { order });

        var result = await rig.Service.GetOrderHistoryAsync(customerId: 7);

        var item = Assert.Single(result);
        Assert.Equal(101, item.OrderId);
        Assert.Equal(42, item.EventId);
        Assert.Equal("Concert A", item.EventName);
        Assert.Equal("https://example.test/poster.jpg", item.PosterUrl);
        Assert.Equal(orderDate, item.OrderDate);
        Assert.Equal(startsAt, item.StartsAt);
        Assert.Equal(500_000, item.TotalAmount);
        Assert.Equal(50_000, item.DiscountAmount);
        Assert.Equal(450_000, item.FinalAmount);
        Assert.Equal("VNPay", item.PaymentMethod);
        Assert.Equal("Paid", item.Status);
        Assert.Equal(expiresAt, item.ExpiresAt);
        Assert.Equal(5, item.TicketCount);

        rig.Repository.Verify(
            repository => repository.GetOrdersByCustomerIdAsync(7),
            Times.Once);
    }

    [Fact]
    public async Task T02_NullDiscountAndNullOrderDetails_DefaultToZero()
    {
        var order = CreateOrder(
            orderId: 102,
            discountAmount: null,
            quantities: null);

        order.OrderDetails = null!;

        using var rig = CreateRig(new[] { order });

        var item = Assert.Single(
            await rig.Service.GetOrderHistoryAsync(customerId: 7));

        Assert.Equal(0, item.DiscountAmount);
        Assert.Equal(0, item.TicketCount);
    }

    [Fact]
    public async Task T03_RepositoryReturnsNoOrders_ReturnsEmptyList()
    {
        using var rig = CreateRig(Array.Empty<Order>());

        var result = await rig.Service.GetOrderHistoryAsync(customerId: 7);

        Assert.Empty(result);
        rig.Repository.Verify(
            repository => repository.GetOrdersByCustomerIdAsync(7),
            Times.Once);
    }

    [Fact]
    public async Task T04_MapsMultipleOrdersAndPreservesRepositoryOrder()
    {
        var laterOrder = CreateOrder(
            orderId: 204,
            eventName: "Later order",
            quantities: new[] { 1 });

        var earlierOrder = CreateOrder(
            orderId: 201,
            eventName: "Earlier order",
            quantities: new[] { 2 });

        using var rig = CreateRig(new[] { laterOrder, earlierOrder });

        var result = await rig.Service.GetOrderHistoryAsync(customerId: 12);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 204, 201 }, result.Select(item => item.OrderId));
        Assert.Equal("Later order", result[0].EventName);
        Assert.Equal("Earlier order", result[1].EventName);
        Assert.Equal(new[] { 1, 2 }, result.Select(item => item.TicketCount));
    }

    [Fact]
    public async Task T05_NullOptionalOrderFieldsRemainNull()
    {
        var order = CreateOrder(
            orderId: 301,
            eventName: null,
            posterUrl: null,
            orderDate: null,
            startsAt: null,
            paymentMethod: null,
            status: null,
            expiresAt: null,
            quantities: Array.Empty<int>());

        using var rig = CreateRig(new[] { order });

        var item = Assert.Single(
            await rig.Service.GetOrderHistoryAsync(customerId: 7));

        Assert.Null(item.EventName);
        Assert.Null(item.PosterUrl);
        Assert.Null(item.OrderDate);
        Assert.Null(item.StartsAt);
        Assert.Null(item.PaymentMethod);
        Assert.Null(item.Status);
        Assert.Null(item.ExpiresAt);
        Assert.Equal(0, item.TicketCount);
    }

    [Fact]
    public async Task T06_RepositoryFailureIsPropagated()
    {
        using var rig = CreateRig(Array.Empty<Order>());

        rig.Repository
            .Setup(repository =>
                repository.GetOrdersByCustomerIdAsync(7))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.GetOrderHistoryAsync(customerId: 7));
    }

    private static Order CreateOrder(
        int orderId,
        int eventId = 42,
        string? eventName = "Test Event",
        string? posterUrl = "https://example.test/poster.jpg",
        DateTime? orderDate = null,
        DateTime? startsAt = null,
        long totalAmount = 100_000,
        long? discountAmount = 0,
        long finalAmount = 100_000,
        string? paymentMethod = "VNPay",
        string? status = "Paid",
        DateTime? expiresAt = null,
        int[]? quantities = null)
    {
        quantities ??= new[] { 1 };

        return new Order
        {
            OrderId = orderId,
            CustomerId = 7,
            EventId = eventId,
            EventName = eventName,
            PosterUrl = posterUrl,
            OrderDate = orderDate,
            StartsAt = startsAt,
            TotalAmount = totalAmount,
            DiscountAmount = discountAmount,
            FinalAmount = finalAmount,
            PaymentMethod = paymentMethod,
            Status = status,
            ExpiresAt = expiresAt,
            OrderDetails = quantities
                .Select((quantity, index) => new OrderDetail
                {
                    OrderDetailId = index + 1,
                    OrderId = orderId,
                    TicketTypeId = index + 1,
                    TicketTypeName = $"Ticket type {index + 1}",
                    Quantity = quantity
                })
                .ToList()
        };
    }

    private static Rig CreateRig(IEnumerable<Order> orders)
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TicketDbContext(options);

        var repository = new Mock<IOrderRepository>();
        repository
            .Setup(item => item.GetOrdersByCustomerIdAsync(
                It.IsAny<int>()))
            .ReturnsAsync(orders.ToList());

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