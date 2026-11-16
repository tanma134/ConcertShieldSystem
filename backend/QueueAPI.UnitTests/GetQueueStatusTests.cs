using QueueAPI.Services;
using QueueAPI.Time;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class GetQueueStatusTests
{
    [Fact]
    public async Task UTCID01_ReturnsRegisteredStatusBeforePreQueueEnds()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 10, "REGISTERED", (DateTimeOffset?)null));

        var result = await context.Service.GetStatusAsync(12, 7, session.QueueToken);

        Assert.Equal("REGISTERED", result.Status);
        Assert.Null(result.Position);
        Assert.Equal(10, result.TotalWaiting);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Redis.Verify(redis => redis.AdmitNextAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID02_MapsPreQueueRedisStatusToRegisteredDatabaseStatus()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 20, "PREQUEUE", (DateTimeOffset?)null));

        var result = await context.Service.GetStatusAsync(12, 7, session.QueueToken);

        Assert.Equal("PREQUEUE", result.Status);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        Assert.Null(result.Position);
    }

    [Fact]
    public async Task UTCID03_ReturnsWaitingPositionAndEstimatedWaitAfterWindowEnds()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1), admissionRate: 10);
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((4, 50, "WAITING", (DateTimeOffset?)null));

        var result = await context.Service.GetStatusAsync(12, 7, session.QueueToken);

        Assert.Equal("WAITING", result.Status);
        Assert.Equal(4, result.Position);
        Assert.Equal(1, result.EstimatedWaitTimeMinutes);
        var savedSession = Assert.Single(context.Db.QueueSessions);
        Assert.Equal("WAITING", savedSession.Status);
        Assert.Equal(4, savedSession.Position);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Redis.Verify(redis => redis.AdmitNextAsync(12, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID04_ReturnsAdmissionExpiryAndNoPositionForAdmittedUser()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));
        var session = await context.SeedSessionAsync();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5);
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 50, "ADMITTED", (DateTimeOffset?)expiry));

        var result = await context.Service.GetStatusAsync(12, 7, session.QueueToken);

        Assert.Equal("ADMITTED", result.Status);
        Assert.Null(result.Position);
        Assert.Equal(expiry, result.AdmissionExpiresAt);
        Assert.Equal("ADMITTED", Assert.Single(context.Db.QueueSessions).Status);
    }

    [Fact]
    public async Task UTCID05_ExpiresPreviouslyAdmittedSessionWhenRedisNoLongerAdmitsIt()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));
        var session = await context.SeedSessionAsync(status: "ADMITTED", position: 4);
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((5, 50, "WAITING", (DateTimeOffset?)null));

        var result = await context.Service.GetStatusAsync(12, 7, session.QueueToken);

        Assert.Equal("EXPIRED", result.Status);
        Assert.Null(result.Position);
        Assert.Null(result.EstimatedWaitTimeMinutes);
        var savedSession = Assert.Single(context.Db.QueueSessions);
        Assert.Equal("EXPIRED", savedSession.Status);
        Assert.Null(savedSession.Position);
    }

    [Fact]
    public async Task UTCID06_FinalizesButDoesNotAdmitWhenQueueIsDisabled()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1), admissionRate: 10, queueEnabled: false);
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((4, 50, "WAITING", (DateTimeOffset?)null));

        var result = await context.Service.GetStatusAsync(12, 7, session.QueueToken);

        Assert.Equal("WAITING", result.Status);
        Assert.Equal(4, result.Position);
        Assert.Equal(1, result.EstimatedWaitTimeMinutes);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Redis.Verify(redis => redis.AdmitNextAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID07_AllowsNullTokenAndUsesStoredToken()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1), admissionRate: 10);
        var session = await context.SeedSessionAsync(token: "T-STORED");
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, "T-STORED", It.IsAny<CancellationToken>()))
            .ReturnsAsync((4, 50, "WAITING", (DateTimeOffset?)null));

        var result = await context.Service.GetStatusAsync(12, 7, null);

        Assert.Equal("T-STORED", result.QueueToken);
        Assert.Equal("WAITING", result.Status);
        Assert.Equal(4, result.Position);
        Assert.Equal(1, result.EstimatedWaitTimeMinutes);
    }

    [Fact]
    public async Task UTCID08_UsesMinimumOneMinuteWaitEstimateAtPositionOne()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1), admissionRate: 100);
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, 50, "WAITING", (DateTimeOffset?)null));

        var result = await context.Service.GetStatusAsync(12, 7, session.QueueToken);

        Assert.Equal(1, result.Position);
        Assert.Equal(1, result.EstimatedWaitTimeMinutes);
    }

    [Fact]
    public async Task UTCID09_RejectsMissingSession()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.GetStatusAsync(12, 7, "T-VALID"));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, "T-VALID", It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID10_RejectsMismatchedToken()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));
        await context.SeedSessionAsync(token: "T-VALID");

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.GetStatusAsync(12, 7, "T-OTHER"));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, "T-OTHER", It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID11_RejectsRevokedSession()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));
        var session = await context.SeedSessionAsync(status: "REVOKED");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.GetStatusAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_REVOKED", exception.Message);
        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            12, session.QueueToken, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID12_ThrowsWhenConfigurationIsMissing()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedSessionAsync();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            context.Service.GetStatusAsync(12, 7, "T-VALID"));

        Assert.Equal("EVENT_NOT_FOUND", exception.Message);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID13_RejectsRedisTokenInvalidAfterWindowEnds()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(end: now.AddMinutes(-1));
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.AdmitNextAsync(12, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        context.Redis.Setup(redis => redis.GetStatusAsync(12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0, "TOKEN_INVALID", (DateTimeOffset?)null));

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.GetStatusAsync(12, 7, session.QueueToken));

        Assert.Equal("TOKEN_INVALID", exception.Message);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        context.Redis.Verify(redis => redis.FinalizeRandomizationAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Redis.Verify(redis => redis.AdmitNextAsync(12, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
