using Microsoft.EntityFrameworkCore;
using QueueAPI.Services;
using QueueAPI.Time;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class RegisterQueueTests
{
    [Fact]
    public async Task UTCID01_ThrowsWhenConfigurationDoesNotExist()
    {
        using var context = new QueueServiceTestContext();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            context.Service.RegisterAsync(12, 7));

        Assert.Equal("EVENT_NOT_FOUND", exception.Message);
        Assert.Empty(context.Db.QueueSessions);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID02_RejectsDisabledQueue()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(queueEnabled: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.RegisterAsync(12, 7));

        Assert.Equal("QUEUE_NOT_AVAILABLE", exception.Message);
        Assert.Empty(context.Db.QueueSessions);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID03_RejectsRegistrationBeforePreQueueOpens()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(start: now.AddMinutes(1), end: now.AddHours(1));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.RegisterAsync(12, 7));

        Assert.Equal("PREQUEUE_NOT_OPEN", exception.Message);
        Assert.Empty(context.Db.QueueSessions);
    }

    [Fact]
    public async Task UTCID04_RejectsRegistrationAfterPreQueueCloses()
    {
        using var context = new QueueServiceTestContext();
        var now = QueueTimeZone.Now;
        await context.SeedConfigurationAsync(start: now.AddHours(-1), end: now.AddMinutes(-1));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.RegisterAsync(12, 7));

        Assert.Equal("PREQUEUE_CLOSED", exception.Message);
        Assert.Empty(context.Db.QueueSessions);
    }

    [Fact]
    public async Task UTCID05_CreatesAndRegistersNewSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        context.Redis.Setup(redis => redis.ResetUserAsync(12, 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await context.Service.RegisterAsync(12, 7);

        var session = Assert.Single(context.Db.QueueSessions);
        Assert.Equal("REGISTERED", session.Status);
        Assert.Matches("^[A-F0-9]{64}$", session.QueueToken);
        Assert.Matches("^[a-f0-9]{32}$", session.SessionId);
        Assert.Equal(session.QueueToken, result.QueueToken);
        Assert.Equal("REGISTERED", result.Status);
        context.Redis.Verify(redis => redis.ResetUserAsync(12, 7, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Redis.Verify(redis => redis.RegisterAsync(
            12, 7, session.QueueToken, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(context.Db.AbuseAlerts);
    }

    [Fact]
    public async Task UTCID06_ReusesLeftSessionWithFreshIdentityAndToken()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var previous = await context.SeedSessionAsync(status: "LEFT", token: "T-OLD", position: 5);
        var previousSessionId = previous.SessionId;
        context.Redis.Setup(redis => redis.ResetUserAsync(12, 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await context.Service.RegisterAsync(12, 7);

        var session = Assert.Single(context.Db.QueueSessions);
        Assert.Equal(previous.QueueSessionId, session.QueueSessionId);
        Assert.Equal("REGISTERED", session.Status);
        Assert.Null(session.Position);
        Assert.NotEqual("T-OLD", session.QueueToken);
        Assert.NotEqual(previousSessionId, session.SessionId);
        Assert.Equal(session.QueueToken, result.QueueToken);
        context.Redis.Verify(redis => redis.ResetUserAsync(12, 7, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(context.Db.AbuseAlerts);
    }

    [Fact]
    public async Task UTCID07_RejectsAlreadyRegisteredSessionWhenRedisReportsItExists()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var existing = await context.SeedSessionAsync(status: "REGISTERED", token: "T-EXISTING");
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, "T-EXISTING", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.RegisterAsync(12, 7));

        Assert.Equal("ALREADY_REGISTERED", exception.Message);
        Assert.Equal("T-EXISTING", Assert.Single(context.Db.QueueSessions).QueueToken);
        Assert.Equal(existing.SessionId, Assert.Single(context.Db.QueueSessions).SessionId);
        context.Redis.Verify(redis => redis.ResetUserAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID08_ReportsRegistrationUnavailableForExistingSessionWhenRedisRejectsIt()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var existing = await context.SeedSessionAsync(status: "REGISTERED", token: "T-EXISTING");
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, "T-EXISTING", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.RegisterAsync(12, 7));

        Assert.Equal("QUEUE_REGISTRATION_UNAVAILABLE", exception.Message);
        Assert.Equal(existing.SessionId, Assert.Single(context.Db.QueueSessions).SessionId);
    }

    [Fact]
    public async Task UTCID09_RejectsRevokedExistingSessionAsAlreadyRegistered()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var existing = await context.SeedSessionAsync(status: "REVOKED", token: "T-REVOKED");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.RegisterAsync(12, 7));

        Assert.Equal("ALREADY_REGISTERED", exception.Message);
        Assert.Equal("REVOKED", Assert.Single(context.Db.QueueSessions).Status);
        Assert.Equal(existing.SessionId, Assert.Single(context.Db.QueueSessions).SessionId);
        context.Redis.Verify(redis => redis.RegisterAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID10_PersistsNewSessionWhenRedisRegistrationFails()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        context.Redis.Setup(redis => redis.ResetUserAsync(12, 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.RegisterAsync(12, 7));

        Assert.Equal("ALREADY_REGISTERED", exception.Message);
        Assert.Equal("REGISTERED", Assert.Single(context.Db.QueueSessions).Status);
        Assert.Empty(context.Db.AbuseAlerts);
    }

    [Fact]
    public async Task UTCID11_DoesNotCombineSessionsAcrossDifferentEvents()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var recent = QueueTimeZone.Now.AddMinutes(-1);
        context.Db.QueueSessions.AddRange(
            QueueServiceTestContext.CreateSession(13, 7, "REGISTERED", "TOKEN-A", createdAt: recent),
            QueueServiceTestContext.CreateSession(14, 7, "WAITING", "TOKEN-B", createdAt: recent));
        await context.Db.SaveChangesAsync();
        context.Redis.Setup(redis => redis.ResetUserAsync(12, 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await context.Service.RegisterAsync(12, 7);

        Assert.Empty(context.Db.AbuseAlerts);
        Assert.Equal(12, Assert.Single(context.Db.QueueSessions.Where(session => session.UserId == 7 && session.EventId == 12)).EventId);
    }

    [Fact]
    public async Task UTCID12_DoesNotCreateEventWideSpikeAlerts()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var recent = QueueTimeZone.Now.AddMinutes(-1);
        context.Db.QueueSessions.AddRange(
            QueueServiceTestContext.CreateSession(12, 8, "REGISTERED", "TOKEN-A", createdAt: recent),
            QueueServiceTestContext.CreateSession(12, 9, "WAITING", "TOKEN-B", createdAt: recent),
            QueueServiceTestContext.CreateSession(12, 10, "ADMITTED", "TOKEN-C", createdAt: recent));
        await context.Db.SaveChangesAsync();
        context.Redis.Setup(redis => redis.ResetUserAsync(12, 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await context.Service.RegisterAsync(12, 7);

        Assert.Empty(context.Db.AbuseAlerts);
    }

    [Fact]
    public async Task UTCID13_DoesNotAlertFromPersistedSessionCounts()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var recent = QueueTimeZone.Now.AddMinutes(-1);
        context.Db.QueueSessions.AddRange(
            QueueServiceTestContext.CreateSession(13, 7, "REGISTERED", "TOKEN-A", createdAt: recent),
            QueueServiceTestContext.CreateSession(12, 8, "REGISTERED", "TOKEN-B", createdAt: recent),
            QueueServiceTestContext.CreateSession(12, 9, "WAITING", "TOKEN-C", createdAt: recent));
        await context.Db.SaveChangesAsync();
        context.Redis.Setup(redis => redis.ResetUserAsync(12, 7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await context.Service.RegisterAsync(12, 7);

        Assert.Empty(context.Db.AbuseAlerts);
    }
}
