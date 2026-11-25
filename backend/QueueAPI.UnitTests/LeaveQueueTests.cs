using QueueAPI.Services;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class LeaveQueueTests
{
    private static readonly DateTime End = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified);
    private const string ExpectedCycleKey = "20261110100000000";

    [Fact]
    public async Task UTCID01_LeavesRegisteredSessionAndUpdatesDatabase()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(end: End);
        var oldUpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var session = await context.SeedSessionAsync(status: "REGISTERED", createdAt: oldUpdatedAt);
        context.Redis.Setup(redis => redis.LeaveAsync(
                12, 7, session.QueueToken, ExpectedCycleKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await context.Service.LeaveAsync(12, 7, session.QueueToken);

        var savedSession = Assert.Single(context.Db.QueueSessions);
        Assert.Equal("LEFT", savedSession.Status);
        Assert.True(savedSession.UpdatedAt > oldUpdatedAt);
        context.Redis.Verify(redis => redis.LeaveAsync(
            12, 7, session.QueueToken, ExpectedCycleKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_LeavesWaitingSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(end: End);
        var oldUpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var session = await context.SeedSessionAsync(status: "WAITING", position: 4, createdAt: oldUpdatedAt);
        context.Redis.Setup(redis => redis.LeaveAsync(
                12, 7, session.QueueToken, ExpectedCycleKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await context.Service.LeaveAsync(12, 7, session.QueueToken);

        var savedSession = Assert.Single(context.Db.QueueSessions);
        Assert.Equal("LEFT", savedSession.Status);
        Assert.Equal(4, savedSession.Position);
        Assert.True(savedSession.UpdatedAt > oldUpdatedAt);
    }

    [Fact]
    public async Task UTCID03_AllowsNullTokenAndUsesStoredToken()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(end: End);
        var oldUpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var session = await context.SeedSessionAsync(token: "T-STORED", createdAt: oldUpdatedAt);
        context.Redis.Setup(redis => redis.LeaveAsync(
                12, 7, "T-STORED", ExpectedCycleKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await context.Service.LeaveAsync(12, 7, null);

        var savedSession = Assert.Single(context.Db.QueueSessions);
        Assert.Equal("LEFT", savedSession.Status);
        Assert.True(savedSession.UpdatedAt > oldUpdatedAt);
        context.Redis.Verify(redis => redis.LeaveAsync(
            12, 7, "T-STORED", ExpectedCycleKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID04_RejectsMissingSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(end: End);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.LeaveAsync(12, 7, "T-VALID"));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, "T-VALID", It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID05_RejectsMismatchedToken()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(end: End);
        var session = await context.SeedSessionAsync(token: "T-VALID");

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.LeaveAsync(12, 7, "T-OTHER"));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, "T-OTHER", It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID06_RejectsAlreadyLeftSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(end: End);
        var session = await context.SeedSessionAsync(status: "LEFT");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.LeaveAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_REVOKED", exception.Message);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID07_RejectsRevokedSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(end: End);
        var session = await context.SeedSessionAsync(status: "REVOKED");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.LeaveAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_REVOKED", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, session.QueueToken, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID08_ThrowsWhenConfigurationIsMissing()
    {
        using var context = new QueueServiceTestContext();
        var session = await context.SeedSessionAsync();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            context.Service.LeaveAsync(12, 7, session.QueueToken));

        Assert.Equal("EVENT_NOT_FOUND", exception.Message);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID09_PreservesDatabaseSessionWhenRedisLeaveFails()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(end: End);
        var session = await context.SeedSessionAsync(status: "WAITING", position: 4);
        context.Redis.Setup(redis => redis.LeaveAsync(
                12, 7, session.QueueToken, ExpectedCycleKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.LeaveAsync(12, 7, session.QueueToken));

        Assert.Equal("Redis unavailable", exception.Message);
        var savedSession = Assert.Single(context.Db.QueueSessions);
        Assert.Equal("WAITING", savedSession.Status);
        Assert.Equal(4, savedSession.Position);
    }
}
