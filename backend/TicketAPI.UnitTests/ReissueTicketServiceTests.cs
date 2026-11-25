using Moq;
using TicketAPI.Domain;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.Tests;

public class ReissueTicketServiceTests
{
    private const int TicketId = 101;
    private const int AdminUserId = 9;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task T01_NonPositiveTicketIdIsRejected(int ticketId)
    {
        using var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ReissueTicketAsync(
                ticketId,
                AdminUserId,
                "QR code was compromised"));

        VerifyRepositoryWasNotCalled(rig);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task T02_NonPositiveAdminIdIsRejected(int adminUserId)
    {
        using var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ReissueTicketAsync(
                TicketId,
                adminUserId,
                "QR code was compromised"));

        VerifyRepositoryWasNotCalled(rig);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task T03_MissingOrWhitespaceReasonIsRejected(string? reason)
    {
        using var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ReissueTicketAsync(
                TicketId,
                AdminUserId,
                reason!));

        VerifyRepositoryWasNotCalled(rig);
    }

    [Fact]
    public async Task T04_ReasonLongerThan500CharactersIsRejected()
    {
        using var rig = CreateRig();
        var reason = new string('R', 501);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ReissueTicketAsync(
                TicketId,
                AdminUserId,
                reason));

        VerifyRepositoryWasNotCalled(rig);
    }

    [Fact]
    public async Task T05_ValidRequestTrimsReasonAndReturnsMappedReplacementTicket()
    {
        using var rig = CreateRig();

        var replacement = CreateTicket(
            ticketId: 202,
            status: TicketStatuses.Active,
            ticketCode: Guid.Parse("11111111-2222-3333-4444-555555555555"));

        rig.TicketRepository
            .Setup(repository => repository.ReissueTicketAsync(
                TicketId,
                AdminUserId,
                "QR code was compromised"))
            .ReturnsAsync(replacement);

        var result = await rig.Service.ReissueTicketAsync(
            TicketId,
            AdminUserId,
            "  QR code was compromised  ");

        Assert.Equal(202, result.TicketId);
        Assert.Equal(replacement.TicketCode, result.TicketCode);
        Assert.Equal(42, result.OrderId);
        Assert.Equal(10, result.EventId);
        Assert.Equal(3, result.TicketTypeId);
        Assert.Equal("VIP", result.TicketTypeName);
        Assert.Equal(55, result.SeatId);
        Assert.Equal("Alex Nguyen", result.OwnerName);
        Assert.Equal(TicketStatuses.Active, result.Status);
        Assert.Equal("Concert Night", result.EventName);
        Assert.Equal("poster.jpg", result.PosterUrl);
        Assert.Equal("Paid", result.OrderStatus);
        Assert.Equal(500000, result.UnitPrice);

        rig.TicketRepository.Verify(
            repository => repository.ReissueTicketAsync(
                TicketId,
                AdminUserId,
                "QR code was compromised"),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task T06_ReasonAtAllowedLengthBoundariesIsAccepted(int length)
    {
        using var rig = CreateRig();
        var reason = new string('R', length);

        await rig.Service.ReissueTicketAsync(
            TicketId,
            AdminUserId,
            reason);

        rig.TicketRepository.Verify(
            repository => repository.ReissueTicketAsync(
                TicketId,
                AdminUserId,
                reason),
            Times.Once);
    }

    [Fact]
    public async Task T07_RepositoryFailureIsPropagated()
    {
        using var rig = CreateRig();

        rig.TicketRepository
            .Setup(repository => repository.ReissueTicketAsync(
                TicketId,
                AdminUserId,
                "QR code was compromised"))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ReissueTicketAsync(
                TicketId,
                AdminUserId,
                "QR code was compromised"));

        rig.TicketRepository.Verify(
            repository => repository.ReissueTicketAsync(
                TicketId,
                AdminUserId,
                "QR code was compromised"),
            Times.Once);
    }

    private static Ticket CreateTicket(
        int ticketId = TicketId,
        string status = TicketStatuses.Active,
        Guid? ticketCode = null)
    {
        var order = new Order
        {
            OrderId = 42,
            EventId = 10,
            EventName = "Concert Night",
            PosterUrl = "poster.jpg",
            StartsAt = new DateTime(2026, 12, 1, 19, 0, 0, DateTimeKind.Utc),
            OrderDate = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            Status = "Paid",
            OrderDetails = new List<OrderDetail>
            {
                new()
                {
                    OrderId = 42,
                    TicketTypeId = 3,
                    TicketTypeName = "VIP",
                    UnitPrice = 500000,
                    Quantity = 1
                }
            }
        };

        return new Ticket
        {
            TicketId = ticketId,
            TicketCode = ticketCode ?? Guid.NewGuid(),
            OrderId = 42,
            EventId = 10,
            TicketTypeId = 3,
            TicketTypeName = "VIP",
            SeatId = 55,
            OwnerUserId = 7,
            OwnerName = "Alex Nguyen",
            Status = status,
            Order = order
        };
    }

    private static Rig CreateRig()
    {
        var repository = new Mock<ITicketRepository>();

        repository
            .Setup(item => item.ReissueTicketAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>()))
            .ReturnsAsync(CreateTicket(ticketId: 202));

        return new Rig(
            new TicketService(repository.Object),
            repository);
    }

    private static void VerifyRepositoryWasNotCalled(Rig rig)
    {
        rig.TicketRepository.Verify(
            repository => repository.ReissueTicketAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>()),
            Times.Never);
    }

    private sealed class Rig : IDisposable
    {
        public Rig(
            TicketService service,
            Mock<ITicketRepository> ticketRepository)
        {
            Service = service;
            TicketRepository = ticketRepository;
        }

        public TicketService Service { get; }
        public Mock<ITicketRepository> TicketRepository { get; }

        public void Dispose()
        {
        }
    }
}