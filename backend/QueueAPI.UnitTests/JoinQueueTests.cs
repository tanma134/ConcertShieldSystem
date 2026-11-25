using QueueAPI.Services;
using QueueAPI.Time;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class JoinQueueTests
{
    [Fact]
    public async Task UTCID01_ReturnsPreQueueStatusBeforeQueueWindowEnds()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.JoinAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 0, 5, "REGISTERED"));

        var result = await context.Service.JoinAsync(12, 7, session.QueueToken);

        Assert.Equal("PREQUEUE", result.Status);
        Assert.Null(result.Position);
        Assert.Equal(5, result.TotalWaiting);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Redis.Verify(redis => redis.AdmitNextAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID02_UpdatesSessionAndQueuePositionAfterWindowEnds()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1), admissionRate: 100);
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.JoinAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 3, 50, "WAITING"));

        var result = await context.Service.JoinAsync(12, 7, session.QueueToken);

        Assert.Equal("WAITING", result.Status);
        Assert.Equal(3, result.Position);
        Assert.Equal(1, result.EstimatedWaitTimeMinutes);
        var savedSession = Assert.Single(context.Db.QueueSessions);
        Assert.Equal("WAITING", savedSession.Status);
        Assert.Equal(3, savedSession.Position);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Redis.Verify(redis => redis.AdmitNextAsync(12, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID03_ClearsPositionForAdmittedSession()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));
        var session = await context.SeedSessionAsync(position: 4);
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.JoinAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 0, 50, "ADMITTED"));

        var result = await context.Service.JoinAsync(12, 7, session.QueueToken);

        Assert.Equal("ADMITTED", result.Status);
        Assert.Null(result.Position);
        Assert.Equal(50, result.TotalWaiting);
        Assert.Null(Assert.Single(context.Db.QueueSessions).Position);
    }

    [Fact]
    public async Task UTCID04_FinalizesButDoesNotAdmitWhenQueueIsDisabled()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1), queueEnabled: false);
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.JoinAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 3, 50, "WAITING"));

        var result = await context.Service.JoinAsync(12, 7, session.QueueToken);

        Assert.Equal("WAITING", result.Status);
        Assert.Equal(3, result.Position);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Redis.Verify(redis => redis.AdmitNextAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID05_AllowsNullTokenAndUsesStoredToken()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));
        var session = await context.SeedSessionAsync(token: "T-STORED");
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.JoinAsync(12, 7, "T-STORED", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 3, 50, "WAITING"));

        var result = await context.Service.JoinAsync(12, 7, null);

        Assert.Equal("T-STORED", result.QueueToken);
        Assert.Equal("WAITING", result.Status);
        Assert.Equal(3, result.Position);
        Assert.Equal(50, result.TotalWaiting);
        Assert.Equal(1, result.EstimatedWaitTimeMinutes);
        Assert.Equal("WAITING", Assert.Single(context.Db.QueueSessions).Status);
    }

    [Fact]
    public async Task UTCID06_UsesMinimumOneMinuteWaitEstimateAtPositionOne()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1), admissionRate: 100);
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.JoinAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 1, 50, "WAITING"));

        var result = await context.Service.JoinAsync(12, 7, session.QueueToken);

        Assert.Equal(1, result.Position);
        Assert.Equal(50, result.TotalWaiting);
        Assert.Equal(1, result.EstimatedWaitTimeMinutes);
        Assert.Equal(1, Assert.Single(context.Db.QueueSessions).Position);
    }

    [Fact]
    public async Task UTCID07_ThrowsWhenConfigurationIsMissing()
    {
        using var context = new QueueServiceTestContext();
        var session = await context.SeedSessionAsync();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            context.Service.JoinAsync(12, 7, session.QueueToken));

        Assert.Equal("EVENT_NOT_FOUND", exception.Message);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID08_RejectsMissingSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.JoinAsync(12, 7, "T-VALID"));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, "T-VALID", It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID09_RejectsMismatchedToken()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        await context.SeedSessionAsync(token: "T-VALID");

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.JoinAsync(12, 7, "T-OTHER"));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, "T-OTHER", It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID10_RejectsLeftSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync(status: "LEFT");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.JoinAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_REVOKED", exception.Message);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID11_RejectsRevokedSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync(status: "REVOKED");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.JoinAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_REVOKED", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, session.QueueToken, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID12_RejectsBlockedSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync(status: "BLOCKED");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.JoinAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_REVOKED", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, session.QueueToken, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID13_RejectsRedisTokenInvalidDuringPreQueue()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.JoinAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, 0, 0, "TOKEN_INVALID"));

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.JoinAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID14_RejectsRedisTokenInvalidAfterWindowEnds()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.JoinAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, 0, 0, "TOKEN_INVALID"));

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.JoinAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
