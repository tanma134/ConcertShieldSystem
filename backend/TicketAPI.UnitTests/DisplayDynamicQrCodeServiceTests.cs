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

public class DisplayDynamicQrCodeServiceTests
{
    private const int TicketId = 101;
    private const int UserId = 7;

    [Fact]
    public async Task T01_NoExistingQrToken_CreatesAndReturnsNewToken()
    {
        var ticket = CreateTicket();
        using var rig = CreateRig(ticket);
        var before = DateTime.UtcNow;

        var result = await rig.Service.RotateTicketQrAsync(
            TicketId,
            UserId);

        var after = DateTime.UtcNow;
        var token = Assert.Single(rig.AddedQrTokens);

        Assert.Equal(TicketId, result.TicketId);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", result.QrToken);
        Assert.Equal(result.QrToken, token.QrToken);
        Assert.Equal(TicketId, token.TicketId);
        Assert.False(token.IsRevoked);
        Assert.False(token.IsUsed);
        Assert.InRange(token.IssuedAt, before, after);
        Assert.Equal(token.IssuedAt.AddSeconds(60), token.ExpiresAt);
        Assert.Equal(token.ExpiresAt, result.ExpiresAtUtc);

        rig.TicketRepository.Verify(
            repository => repository.AddQrTokensAsync(
                It.IsAny<IEnumerable<TicketQrToken>>()),
            Times.Once);

        VerifyTicketLookup(rig);
    }

    [Fact]
    public async Task T02_ExistingActiveQrToken_IsRotatedAndUsageStateIsReset()
    {
        var oldUsedAt = DateTime.UtcNow.AddMinutes(-1);
        var oldRevokedAt = DateTime.UtcNow.AddMinutes(-2);

        var existingToken = new TicketQrToken
        {
            TicketQrTokenId = 501,
            TicketId = TicketId,
            QrToken = "old-qr-token",
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddMinutes(1),
            IsRevoked = false,
            IsUsed = true,
            UsedAt = oldUsedAt,
            RevokedAt = oldRevokedAt
        };

        var ticket = CreateTicket(existingToken);
        using var rig = CreateRig(ticket);

        var result = await rig.Service.RotateTicketQrAsync(
            TicketId,
            UserId);

        Assert.Equal(TicketId, result.TicketId);
        Assert.NotEqual("old-qr-token", result.QrToken);
        Assert.Equal(result.QrToken, existingToken.QrToken);
        Assert.False(existingToken.IsUsed);
        Assert.Null(existingToken.UsedAt);
        Assert.Null(existingToken.RevokedAt);
        Assert.False(existingToken.IsRevoked);
        Assert.Equal(
            existingToken.IssuedAt.AddSeconds(60),
            existingToken.ExpiresAt);
        Assert.Empty(rig.AddedQrTokens);

        rig.TicketRepository.Verify(
            repository => repository.AddQrTokensAsync(
                It.IsAny<IEnumerable<TicketQrToken>>()),
            Times.Never);

        VerifyTicketLookup(rig);
    }

    [Fact]
    public async Task T03_LatestNonRevokedToken_IsRotated()
    {
        var olderToken = new TicketQrToken
        {
            TicketQrTokenId = 601,
            TicketId = TicketId,
            QrToken = "older-token",
            IssuedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-9),
            IsRevoked = false,
            IsUsed = false
        };

        var latestToken = new TicketQrToken
        {
            TicketQrTokenId = 602,
            TicketId = TicketId,
            QrToken = "latest-token",
            IssuedAt = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            IsRevoked = false,
            IsUsed = true,
            UsedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var revokedToken = new TicketQrToken
        {
            TicketQrTokenId = 603,
            TicketId = TicketId,
            QrToken = "revoked-token",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(1),
            IsRevoked = true,
            IsUsed = false
        };

        var ticket = CreateTicket(
            olderToken,
            latestToken,
            revokedToken);

        using var rig = CreateRig(ticket);

        var result = await rig.Service.RotateTicketQrAsync(
            TicketId,
            UserId);

        Assert.NotEqual("latest-token", result.QrToken);
        Assert.Equal(result.QrToken, latestToken.QrToken);
        Assert.Equal("older-token", olderToken.QrToken);
        Assert.Equal("revoked-token", revokedToken.QrToken);
        Assert.True(revokedToken.IsRevoked);
        Assert.False(latestToken.IsUsed);
        Assert.Null(latestToken.UsedAt);
        Assert.Empty(rig.AddedQrTokens);
    }

    [Fact]
    public async Task T04_OnlyRevokedTokensExist_CreatesNewQrToken()
    {
        var revokedToken = new TicketQrToken
        {
            TicketQrTokenId = 701,
            TicketId = TicketId,
            QrToken = "revoked-token",
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow,
            IsRevoked = true,
            IsUsed = false
        };

        var ticket = CreateTicket(revokedToken);
        using var rig = CreateRig(ticket);

        var result = await rig.Service.RotateTicketQrAsync(
            TicketId,
            UserId);

        var addedToken = Assert.Single(rig.AddedQrTokens);

        Assert.Matches("^[A-Za-z0-9_-]{43}$", result.QrToken);
        Assert.Equal(result.QrToken, addedToken.QrToken);
        Assert.Equal(TicketId, addedToken.TicketId);
        Assert.True(revokedToken.IsRevoked);
        Assert.Equal("revoked-token", revokedToken.QrToken);

        rig.TicketRepository.Verify(
            repository => repository.AddQrTokensAsync(
                It.IsAny<IEnumerable<TicketQrToken>>()),
            Times.Once);
    }

    [Fact]
    public async Task T05_UnpaidOrder_IsRejected()
    {
        var ticket = CreateTicket();
        ticket.Order.Status = "Pending";

        using var rig = CreateRig(ticket);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RotateTicketQrAsync(TicketId, UserId));

        Assert.Empty(rig.AddedQrTokens);
        VerifyTicketLookup(rig);
    }

    [Theory]
    [InlineData("Revoked")]
    [InlineData("Used")]
    [InlineData("Cancelled")]
    public async Task T06_NonActiveTicket_IsRejected(string ticketStatus)
    {
        var ticket = CreateTicket();
        ticket.Status = ticketStatus;

        using var rig = CreateRig(ticket);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RotateTicketQrAsync(TicketId, UserId));

        Assert.Empty(rig.AddedQrTokens);
        VerifyTicketLookup(rig);
    }

    [Fact]
    public async Task T07_CheckedInTicket_IsRejected()
    {
        var ticket = CreateTicket();
        ticket.CheckedInAt = DateTime.UtcNow.AddMinutes(-5);

        using var rig = CreateRig(ticket);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RotateTicketQrAsync(TicketId, UserId));

        Assert.Empty(rig.AddedQrTokens);
        VerifyTicketLookup(rig);
    }

    [Fact]
    public async Task T08_TicketNotFoundOrNotOwnedByUser_ThrowsKeyNotFoundException()
    {
        using var rig = CreateRig(ticket: null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.RotateTicketQrAsync(TicketId, UserId));

        Assert.Empty(rig.AddedQrTokens);
        VerifyTicketLookup(rig);
    }

    [Fact]
    public async Task T09_RepositoryFailure_IsPropagated()
    {
        using var rig = CreateRig(ticket: null);

        rig.TicketRepository
            .Setup(repository => repository.GetTicketForQrRotationAsync(
                TicketId,
                UserId))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RotateTicketQrAsync(TicketId, UserId));
    }

    private static Ticket CreateTicket(
        params TicketQrToken[] qrTokens)
    {
        var order = new Order
        {
            OrderId = 42,
            CustomerId = UserId,
            EventId = 10,
            Status = "Paid"
        };

        var ticket = new Ticket
        {
            TicketId = TicketId,
            TicketCode = Guid.NewGuid(),
            OrderId = order.OrderId,
            EventId = order.EventId,
            TicketTypeId = 3,
            OwnerUserId = UserId,
            Status = "Active",
            CheckedInAt = null,
            Order = order,
            TicketQrTokens = qrTokens.ToList()
        };

        foreach (var token in ticket.TicketQrTokens)
        {
            token.TicketId = TicketId;
            token.Ticket = ticket;
        }

        return ticket;
    }

    private static Rig CreateRig(Ticket? ticket)
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TicketDbContext(options);

        var ticketRepository = new Mock<ITicketRepository>();
        ticketRepository
            .Setup(repository => repository.GetTicketForQrRotationAsync(
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync(ticket);

        var addedQrTokens = new List<TicketQrToken>();

        ticketRepository
            .Setup(repository => repository.AddQrTokensAsync(
                It.IsAny<IEnumerable<TicketQrToken>>()))
            .Callback<IEnumerable<TicketQrToken>>(tokens =>
                addedQrTokens.AddRange(tokens))
            .Returns(Task.CompletedTask);

        var service = new OrderService(
            context,
            Mock.Of<IEventApiClient>(),
            Mock.Of<IHoldService>(),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IOrderRepository>(),
            NullLogger<HoldService>.Instance,
            Mock.Of<IEmailService>(),
            ticketRepository.Object,
            Mock.Of<IPaymentAPIClient>(),
            Mock.Of<IStatusHistoryService>());

        return new Rig(
            context,
            service,
            ticketRepository,
            addedQrTokens);
    }

    private static void VerifyTicketLookup(Rig rig)
    {
        rig.TicketRepository.Verify(
            repository => repository.GetTicketForQrRotationAsync(
                TicketId,
                UserId),
            Times.Once);
    }

    private sealed class Rig : IDisposable
    {
        public Rig(
            TicketDbContext context,
            OrderService service,
            Mock<ITicketRepository> ticketRepository,
            List<TicketQrToken> addedQrTokens)
        {
            Context = context;
            Service = service;
            TicketRepository = ticketRepository;
            AddedQrTokens = addedQrTokens;
        }

        public TicketDbContext Context { get; }
        public OrderService Service { get; }
        public Mock<ITicketRepository> TicketRepository { get; }
        public List<TicketQrToken> AddedQrTokens { get; }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}