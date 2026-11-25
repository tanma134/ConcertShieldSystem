using Moq;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.Tests;

public class ViewTicketDetailsServiceTests
{
    private const int TicketId = 101;
    private const int UserId = 7;

    [Fact]
    public async Task T01_OwnerGetsTicketDetailsWithMatchingTicketPrice()
    {
        var checkedInAt = new DateTime(
            2026, 11, 15, 18, 30, 0, DateTimeKind.Utc);

        var ticket = CreateTicket(
            ticketTypeId: 4,
            seatId: 205,
            ownerName: "Attendee One",
            status: "Active",
            checkedInAt: checkedInAt);

        var repository = CreateRepository(ticket);
        var service = new TicketService(repository.Object);

        var result = await service.GetMyTicketDetailsAsync(
            ticketId: TicketId,
            userId: UserId);

        Assert.NotNull(result);
        Assert.Equal(TicketId, result.TicketId);
        Assert.Equal(ticket.TicketCode, result.TicketCode);
        Assert.Equal(42, result.OrderId);
        Assert.Equal(10, result.EventId);
        Assert.Equal(4, result.TicketTypeId);
        Assert.Equal("VIP", result.TicketTypeName);
        Assert.Equal(205, result.SeatId);
        Assert.Equal("Attendee One", result.OwnerName);
        Assert.Equal("Active", result.Status);
        Assert.Equal(checkedInAt, result.CheckedInAt);
        Assert.Equal("Concert Shield Live", result.EventName);
        Assert.Equal("https://example.test/poster.jpg", result.PosterUrl);
        Assert.Equal(
            new DateTime(2026, 11, 15, 19, 0, 0, DateTimeKind.Utc),
            result.StartsAt);
        Assert.Equal(
            new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            result.OrderDate);
        Assert.Equal("Paid", result.OrderStatus);
        Assert.Equal(500_000, result.UnitPrice);

        repository.Verify(
            repo => repo.GetMyTicketByIdAsync(TicketId, UserId),
            Times.Once);
    }

    [Fact]
    public async Task T02_TicketNotFoundOrNotOwnedByUser_ReturnsNull()
    {
        var repository = CreateRepository(ticket: null);
        var service = new TicketService(repository.Object);

        var result = await service.GetMyTicketDetailsAsync(
            ticketId: TicketId,
            userId: UserId);

        Assert.Null(result);

        repository.Verify(
            repo => repo.GetMyTicketByIdAsync(TicketId, UserId),
            Times.Once);
    }

    [Fact]
    public async Task T03_NoMatchingTicketTypeOrderDetail_ReturnsNullUnitPrice()
    {
        var ticket = CreateTicket(ticketTypeId: 99);
        var service = new TicketService(
            CreateRepository(ticket).Object);

        var result = await service.GetMyTicketDetailsAsync(
            ticketId: TicketId,
            userId: UserId);

        Assert.NotNull(result);
        Assert.Equal(99, result.TicketTypeId);
        Assert.Null(result.UnitPrice);
    }

    [Fact]
    public async Task T04_OrderHasNoDetails_ReturnsNullUnitPrice()
    {
        var ticket = CreateTicket(ticketTypeId: 4);
        ticket.Order.OrderDetails.Clear();

        var service = new TicketService(
            CreateRepository(ticket).Object);

        var result = await service.GetMyTicketDetailsAsync(
            ticketId: TicketId,
            userId: UserId);

        Assert.NotNull(result);
        Assert.Null(result.UnitPrice);
    }

    [Fact]
    public async Task T05_NullOptionalTicketAndOrderFieldsArePreserved()
    {
        var ticket = CreateTicket(
            ticketTypeId: 4,
            seatId: null,
            ownerName: null,
            checkedInAt: null);

        ticket.TicketTypeName = null;
        ticket.Order.EventName = null;
        ticket.Order.PosterUrl = null;
        ticket.Order.StartsAt = null;
        ticket.Order.OrderDate = null;
        ticket.Order.Status = null;

        var service = new TicketService(
            CreateRepository(ticket).Object);

        var result = await service.GetMyTicketDetailsAsync(
            ticketId: TicketId,
            userId: UserId);

        Assert.NotNull(result);
        Assert.Null(result.TicketTypeName);
        Assert.Null(result.SeatId);
        Assert.Null(result.OwnerName);
        Assert.Null(result.CheckedInAt);
        Assert.Null(result.EventName);
        Assert.Null(result.PosterUrl);
        Assert.Null(result.StartsAt);
        Assert.Null(result.OrderDate);
        Assert.Null(result.OrderStatus);
        Assert.Equal(500_000, result.UnitPrice);
    }

    [Fact]
    public async Task T06_RepositoryFailure_IsPropagated()
    {
        var repository = new Mock<ITicketRepository>();
        repository
            .Setup(repo => repo.GetMyTicketByIdAsync(TicketId, UserId))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        var service = new TicketService(repository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetMyTicketDetailsAsync(
                ticketId: TicketId,
                userId: UserId));
    }

    private static Mock<ITicketRepository> CreateRepository(Ticket? ticket)
    {
        var repository = new Mock<ITicketRepository>();

        repository
            .Setup(repo => repo.GetMyTicketByIdAsync(
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync(ticket);

        return repository;
    }

    private static Ticket CreateTicket(
        int ticketTypeId = 4,
        int? seatId = 205,
        string? ownerName = "Attendee One",
        string status = "Active",
        DateTime? checkedInAt = null)
    {
        var order = new Order
        {
            OrderId = 42,
            CustomerId = UserId,
            EventId = 10,
            EventName = "Concert Shield Live",
            PosterUrl = "https://example.test/poster.jpg",
            OrderDate = new DateTime(
                2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            StartsAt = new DateTime(
                2026, 11, 15, 19, 0, 0, DateTimeKind.Utc),
            Status = "Paid"
        };

        order.OrderDetails.Add(new OrderDetail
        {
            OrderDetailId = 1,
            OrderId = order.OrderId,
            TicketTypeId = 4,
            TicketTypeName = "VIP",
            Quantity = 1,
            UnitPrice = 500_000,
            Order = order
        });

        var ticket = new Ticket
        {
            TicketId = TicketId,
            TicketCode = Guid.NewGuid(),
            OrderId = order.OrderId,
            EventId = order.EventId,
            TicketTypeId = ticketTypeId,
            TicketTypeName = ticketTypeId == 4
                ? "VIP"
                : $"Type {ticketTypeId}",
            SeatId = seatId,
            OwnerUserId = UserId,
            OwnerName = ownerName,
            Status = status,
            CheckedInAt = checkedInAt,
            Order = order
        };

        return ticket;
    }
}