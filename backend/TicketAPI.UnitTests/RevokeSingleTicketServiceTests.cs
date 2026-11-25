using Moq;
using TicketAPI.Domain;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.Tests;

public class RevokeSingleTicketServiceTests
{
    private const int TicketId = 101;
    private const int AdminUserId = 9;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task T01_NonPositiveTicketIdIsRejected(int ticketId)
    {
        using var rig = CreateRig(CreateTicket());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.RevokeSingleAsync(
                ticketId,
                AdminUserId,
                "Duplicate ticket"));

        VerifyTicketWasNotLoaded(rig);
        VerifyTicketWasNotRevoked(rig);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task T02_NonPositiveAdminIdIsRejected(int adminUserId)
    {
        using var rig = CreateRig(CreateTicket());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                adminUserId,
                "Duplicate ticket"));

        VerifyTicketWasNotLoaded(rig);
        VerifyTicketWasNotRevoked(rig);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task T03_MissingOrWhitespaceReasonIsRejected(string? reason)
    {
        using var rig = CreateRig(CreateTicket());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                AdminUserId,
                reason!));

        VerifyTicketWasNotLoaded(rig);
        VerifyTicketWasNotRevoked(rig);
    }

    [Fact]
    public async Task T04_ReasonLongerThan500CharactersIsRejected()
    {
        using var rig = CreateRig(CreateTicket());
        var reason = new string('R', 501);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                AdminUserId,
                reason));

        VerifyTicketWasNotLoaded(rig);
        VerifyTicketWasNotRevoked(rig);
    }

    [Fact]
    public async Task T05_TicketDoesNotExist_ThrowsKeyNotFoundException()
    {
        using var rig = CreateRig(ticket: null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket"));

        VerifyTicketWasNotRevoked(rig);
    }

    [Fact]
    public async Task T06_CheckedInTicketIsRejected()
    {
        var ticket = CreateTicket(
            checkedInAt: DateTime.UtcNow.AddMinutes(-5));
        using var rig = CreateRig(ticket);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket"));

        VerifyTicketWasNotRevoked(rig);
    }

    [Fact]
    public async Task T07_TicketWithUsedQrTokenIsRejected()
    {
        var usedToken = new TicketQrToken
        {
            TicketQrTokenId = 501,
            TicketId = TicketId,
            QrToken = "used-qr-token",
            IssuedAt = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(1),
            IsUsed = true,
            UsedAt = DateTime.UtcNow.AddMinutes(-1),
            IsRevoked = false
        };

        var ticket = CreateTicket(qrTokens: new[] { usedToken });
        using var rig = CreateRig(ticket);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket"));

        VerifyTicketWasNotRevoked(rig);
    }

    [Theory]
    [InlineData("Revoked")]
    [InlineData("ReturnPending")]
    [InlineData("Returned")]
    public async Task T08_NonActiveTicketIsRejected(string status)
    {
        using var rig = CreateRig(CreateTicket(status: status));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket"));

        VerifyTicketWasNotRevoked(rig);
    }

    [Fact]
    public async Task T09_ActiveTicketIsRevokedWithTrimmedReason()
    {
        using var rig = CreateRig(CreateTicket());

        await rig.Service.RevokeSingleAsync(
            TicketId,
            AdminUserId,
            "  Duplicate ticket confirmed  ");

        rig.TicketRepository.Verify(
            repository => repository.RevokeTicketAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket confirmed"),
            Times.Once);

        rig.TicketRepository.Verify(
            repository => repository.GetTicketForAdminRevokeAsync(TicketId),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public async Task T10_ReasonAtAllowedLengthBoundariesIsAccepted(int length)
    {
        using var rig = CreateRig(CreateTicket());
        var reason = new string('R', length);

        await rig.Service.RevokeSingleAsync(
            TicketId,
            AdminUserId,
            reason);

        rig.TicketRepository.Verify(
            repository => repository.RevokeTicketAsync(
                TicketId,
                AdminUserId,
                reason),
            Times.Once);
    }

    [Fact]
    public async Task T11_TicketLookupRepositoryFailureIsPropagated()
    {
        using var rig = CreateRig(CreateTicket());

        rig.TicketRepository
            .Setup(repository =>
                repository.GetTicketForAdminRevokeAsync(TicketId))
            .ThrowsAsync(new InvalidOperationException(
                "Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket"));

        VerifyTicketWasNotRevoked(rig);
    }

    [Fact]
    public async Task T12_RevokeRepositoryFailureIsPropagated()
    {
        using var rig = CreateRig(CreateTicket());

        rig.TicketRepository
            .Setup(repository => repository.RevokeTicketAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket"))
            .ThrowsAsync(new InvalidOperationException(
                "Could not revoke ticket"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.RevokeSingleAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket"));

        rig.TicketRepository.Verify(
            repository => repository.RevokeTicketAsync(
                TicketId,
                AdminUserId,
                "Duplicate ticket"),
            Times.Once);
    }

    private static Ticket CreateTicket(
        string status = TicketStatuses.Active,
        DateTime? checkedInAt = null,
        IEnumerable<TicketQrToken>? qrTokens = null)
    {
        return new Ticket
        {
            TicketId = TicketId,
            TicketCode = Guid.NewGuid(),
            OrderId = 42,
            EventId = 10,
            TicketTypeId = 3,
            OwnerUserId = 7,
            Status = status,
            CheckedInAt = checkedInAt,
            TicketQrTokens = qrTokens?.ToList()
                ?? new List<TicketQrToken>()
        };
    }

    private static Rig CreateRig(Ticket? ticket)
    {
        var repository = new Mock<ITicketRepository>();

        repository
            .Setup(item => item.GetTicketForAdminRevokeAsync(
                It.IsAny<int>()))
            .ReturnsAsync(ticket);

        repository
            .Setup(item => item.RevokeTicketAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return new Rig(
            new TicketService(repository.Object),
            repository);
    }

    private static void VerifyTicketWasNotLoaded(Rig rig)
    {
        rig.TicketRepository.Verify(
            repository => repository.GetTicketForAdminRevokeAsync(
                It.IsAny<int>()),
            Times.Never);
    }

    private static void VerifyTicketWasNotRevoked(Rig rig)
    {
        rig.TicketRepository.Verify(
            repository => repository.RevokeTicketAsync(
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