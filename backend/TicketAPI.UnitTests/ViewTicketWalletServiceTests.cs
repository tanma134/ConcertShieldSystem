using Moq;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.Tests;

public class ViewTicketWalletServiceTests
{
    [Fact]
    public async Task T01_MapsTicketAndOrderFields_AndUsesMatchingTicketTypePrice()
    {
        var order = CreateOrder(
            orderId: 42,
            eventId: 10,
            status: "Paid",
            eventName: "Concert Shield Live",
            posterUrl: "https://example.test/poster.jpg",
            orderDate: new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            startsAt: new DateTime(2026, 11, 15, 19, 0, 0, DateTimeKind.Utc),
            details: new[]
            {
                new OrderDetail
                {
                    OrderDetailId = 1,
                    OrderId = 42,
                    TicketTypeId = 3,
                    TicketTypeName = "Standard",
                    Quantity = 1,
                    UnitPrice = 200_000
                },
                new OrderDetail
                {
                    OrderDetailId = 2,
                    OrderId = 42,
                    TicketTypeId = 4,
                    TicketTypeName = "VIP",
                    Quantity = 1,
                    UnitPrice = 500_000
                }
            });

        var checkedInAt = new DateTime(
            2026, 11, 15, 18, 30, 0, DateTimeKind.Utc);

        var ticket = CreateTicket(
            ticketId: 101,
            order,
            ticketTypeId: 4,
            seatId: 205,
            ownerName: "Attendee One",
            status: "CheckedIn",
            checkedInAt: checkedInAt);

        var repository = CreateRepository(ticket);
        var service = CreateService(repository.Object);

        var result = await service.GetMyTicketsAsync(userId: 7);

        var item = Assert.Single(result);
        Assert.Equal(101, item.TicketId);
        Assert.Equal(ticket.TicketCode, item.TicketCode);
        Assert.Equal(42, item.OrderId);
        Assert.Equal(10, item.EventId);
        Assert.Equal(4, item.TicketTypeId);
        Assert.Equal("Type 4", item.TicketTypeName);
        Assert.Equal(205, item.SeatId);
        Assert.Equal("Attendee One", item.OwnerName);
        Assert.Equal("CheckedIn", item.Status);
        Assert.Equal(checkedInAt, item.CheckedInAt);
        Assert.Equal("Concert Shield Live", item.EventName);
        Assert.Equal("https://example.test/poster.jpg", item.PosterUrl);
        Assert.Equal(order.StartsAt, item.StartsAt);
        Assert.Equal(order.OrderDate, item.OrderDate);
        Assert.Equal("Paid", item.OrderStatus);
        Assert.Equal(500_000, item.UnitPrice);

        repository.Verify(
            repo => repo.GetMyTicketsAsync(7),
            Times.Once);
    }

    [Fact]
    public async Task T02_NoMatchingTicketTypeDetail_ReturnsNullUnitPrice()
    {
        var order = CreateOrder(
            orderId: 43,
            details: new[]
            {
                new OrderDetail
                {
                    OrderDetailId = 1,
                    OrderId = 43,
                    TicketTypeId = 4,
                    TicketTypeName = "VIP",
                    Quantity = 1,
                    UnitPrice = 500_000
                }
            });

        var ticket = CreateTicket(
            ticketId: 102,
            order,
            ticketTypeId: 3);

        var service = CreateService(
            CreateRepository(ticket).Object);

        var item = Assert.Single(
            await service.GetMyTicketsAsync(userId: 7));

        Assert.Equal(3, item.TicketTypeId);
        Assert.Null(item.UnitPrice);
    }

    [Fact]
    public async Task T03_OrderHasNoDetails_ReturnsNullUnitPrice()
    {
        var order = CreateOrder(
            orderId: 44,
            details: Array.Empty<OrderDetail>());

        var ticket = CreateTicket(
            ticketId: 103,
            order,
            ticketTypeId: 3);

        var service = CreateService(
            CreateRepository(ticket).Object);

        var item = Assert.Single(
            await service.GetMyTicketsAsync(userId: 7));

        Assert.Null(item.UnitPrice);
    }

    [Fact]
    public async Task T04_NoTickets_ReturnsEmptyList()
    {
        var repository = CreateRepository();
        var service = CreateService(repository.Object);

        var result = await service.GetMyTicketsAsync(userId: 7);

        Assert.Empty(result);
        repository.Verify(
            repo => repo.GetMyTicketsAsync(7),
            Times.Once);
    }

    [Fact]
    public async Task T05_MultipleTickets_AreMappedInRepositoryOrder()
    {
        var firstOrder = CreateOrder(
            orderId: 50,
            eventName: "First Concert",
            details: new[]
            {
                new OrderDetail
                {
                    OrderDetailId = 1,
                    OrderId = 50,
                    TicketTypeId = 3,
                    TicketTypeName = "Standard",
                    Quantity = 1,
                    UnitPrice = 200_000
                }
            });

        var secondOrder = CreateOrder(
            orderId: 51,
            eventName: "Second Concert",
            details: new[]
            {
                new OrderDetail
                {
                    OrderDetailId = 2,
                    OrderId = 51,
                    TicketTypeId = 4,
                    TicketTypeName = "VIP",
                    Quantity = 1,
                    UnitPrice = 500_000
                }
            });

        var firstTicket = CreateTicket(
            ticketId: 201,
            firstOrder,
            ticketTypeId: 3,
            seatId: null,
            status: "Active");

        var secondTicket = CreateTicket(
            ticketId: 202,
            secondOrder,
            ticketTypeId: 4,
            seatId: 305,
            status: "Used");

        var service = CreateService(
            CreateRepository(firstTicket, secondTicket).Object);

        var result = await service.GetMyTicketsAsync(userId: 7);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 201, 202 }, result.Select(ticket => ticket.TicketId));
        Assert.Equal(
            new[] { "First Concert", "Second Concert" },
            result.Select(ticket => ticket.EventName));
        Assert.Null(result[0].SeatId);
        Assert.Equal(305, result[1].SeatId);
        Assert.Equal(new long?[] { 200_000, 500_000 },
            result.Select(ticket => ticket.UnitPrice));
    }

    [Fact]
    public async Task T06_RepositoryFailure_IsPropagated()
    {
        var repository = new Mock<ITicketRepository>();
        repository
            .Setup(repo => repo.GetMyTicketsAsync(7))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetMyTicketsAsync(userId: 7));
    }

    [Fact]
    public async Task T07_SuppliedUserIdIsPassedToRepository()
    {
        var repository = CreateRepository();
        var service = CreateService(repository.Object);

        await service.GetMyTicketsAsync(userId: 123);

        repository.Verify(
            repo => repo.GetMyTicketsAsync(123),
            Times.Once);
    }

    private static TicketService CreateService(
        ITicketRepository repository)
    {
        return new TicketService(repository);
    }

    private static Mock<ITicketRepository> CreateRepository(
        params Ticket[] tickets)
    {
        var repository = new Mock<ITicketRepository>();

        repository
            .Setup(repo => repo.GetMyTicketsAsync(It.IsAny<int>()))
            .ReturnsAsync(tickets.ToList());

        return repository;
    }

    private static Order CreateOrder(
        int orderId,
        int eventId = 10,
        string status = "Paid",
        string? eventName = "Test Concert",
        string? posterUrl = "https://example.test/poster.jpg",
        DateTime? orderDate = null,
        DateTime? startsAt = null,
        IEnumerable<OrderDetail>? details = null)
    {
        var order = new Order
        {
            OrderId = orderId,
            CustomerId = 7,
            EventId = eventId,
            EventName = eventName,
            PosterUrl = posterUrl,
            OrderDate = orderDate ??
                        new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            StartsAt = startsAt ??
                       new DateTime(2026, 11, 15, 19, 0, 0, DateTimeKind.Utc),
            Status = status,
            OrderDetails = details?.ToList() ?? new List<OrderDetail>()
        };

        return order;
    }

    private static Ticket CreateTicket(
        int ticketId,
        Order order,
        int ticketTypeId,
        int? seatId = 101,
        string? ownerName = "Ticket Holder",
        string status = "Active",
        DateTime? checkedInAt = null)
    {
        return new Ticket
        {
            TicketId = ticketId,
            TicketCode = Guid.NewGuid(),
            OrderId = order.OrderId,
            EventId = order.EventId,
            TicketTypeId = ticketTypeId,
            TicketTypeName = $"Type {ticketTypeId}",
            SeatId = seatId,
            OwnerUserId = order.CustomerId,
            OwnerName = ownerName,
            Status = status,
            CheckedInAt = checkedInAt,
            Order = order
        };
    }
}