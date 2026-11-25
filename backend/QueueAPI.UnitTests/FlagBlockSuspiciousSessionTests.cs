using Microsoft.AspNetCore.Mvc;
using QueueAPI.DTOs;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class FlagBlockSuspiciousSessionTests
{
    private const string CycleKey = "20261110100000000";

    [Fact]
    public async Task FlagUpdatesRelatedAlertWithoutChangingQueueSessionOrCreatingAnotherAlert()
    {
        using var context = new QueueAdminTestContext();
        var session = await context.SeedSessionAsync();
        var alert = await context.SeedAlertAsync(
            1, "LOW", "OPEN", DateTime.UtcNow, eventId: 12, userId: 7, sessionId: session.SessionId,
            alertType: "MULTIPLE_REGISTRATION_ATTEMPTS", detectedBehavior: "evidence");

        var result = await context.Controller.Flag(
            session.SessionId, new SessionActionRequest("Admin review", alert.AbuseAlertId));

        Assert.Equal("FLAGGED", GetStatus(result));
        Assert.Equal("WAITING", session.Status);
        Assert.Equal("FLAGGED", alert.Status);
        Assert.Equal("evidence", alert.DetectedBehavior);
        Assert.Single(context.Db.AbuseAlerts);
        context.AuditLog.Verify(audit => audit.LogAsync(
            0, "QUEUE_ABUSE_ALERT_FLAGGED", "QueueAbuseAlert", alert.AbuseAlertId.ToString(),
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FlagDoesNotCreateAnAlertWhenNoDetectionAlertExists()
    {
        using var context = new QueueAdminTestContext();
        var session = await context.SeedSessionAsync();

        var result = await context.Controller.Flag(session.SessionId, new SessionActionRequest("Review"));

        Assert.Equal("FLAGGED", GetStatus(result));
        Assert.Equal("WAITING", session.Status);
        Assert.Empty(context.Db.AbuseAlerts);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BlockRevokesSessionAndUpdatesOnlyTheRelatedAlertStatus()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        var alert = await context.SeedAlertAsync(
            1, "HIGH", "OPEN", DateTime.UtcNow, eventId: 12, userId: 7, sessionId: session.SessionId,
            alertType: "TOKEN_REPLAY", detectedBehavior: "evidence");
        context.Redis.Setup(redis => redis.RevokeAsync(
                12, 7, session.SessionId, "TKN1", CycleKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await context.Controller.Block(
            session.SessionId, new SessionActionRequest("Admin block", alert.AbuseAlertId));

        Assert.Equal("BLOCKED", GetStatus(result));
        Assert.Equal("BLOCKED", session.Status);
        Assert.Equal("BLOCKED", alert.Status);
        Assert.Equal("TOKEN_REPLAY", alert.AlertType);
        Assert.Equal("HIGH", alert.Severity);
        Assert.Single(context.Db.AbuseAlerts);
        context.AuditLog.Verify(audit => audit.LogAsync(
            0, "QUEUE_ABUSE_ALERT_BLOCKED", "QueueAbuseAlert", alert.AbuseAlertId.ToString(),
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        context.Redis.Verify(redis => redis.RevokeAsync(
            12, 7, session.SessionId, "TKN1", CycleKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnblockSetsSessionLeftAndReopensOnlyTheRelatedAlert()
    {
        using var context = new QueueAdminTestContext();
        var session = await context.SeedSessionAsync(status: "BLOCKED");
        var alert = await context.SeedAlertAsync(
            1, "HIGH", "BLOCKED", DateTime.UtcNow, eventId: 12, userId: 7, sessionId: session.SessionId,
            alertType: "TOKEN_REPLAY");

        var result = await context.Controller.Unblock(session.SessionId, alert.AbuseAlertId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("LEFT", session.Status);
        Assert.Equal("OPEN", alert.Status);
        Assert.Equal("LEFT", ok.Value!.GetType().GetProperty("sessionStatus")!.GetValue(ok.Value));
        Assert.Single(context.Db.AbuseAlerts);
        context.AuditLog.Verify(audit => audit.LogAsync(
            0, "QUEUE_ABUSE_ALERT_UNBLOCKED", "QueueAbuseAlert", alert.AbuseAlertId.ToString(),
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReturnsNotFoundWhenAlertDoesNotBelongToSession()
    {
        using var context = new QueueAdminTestContext();
        var session = await context.SeedSessionAsync();
        var alert = await context.SeedAlertAsync(
            1, "LOW", "OPEN", DateTime.UtcNow, sessionId: "OTHER");

        var result = await context.Controller.Flag(
            session.SessionId, new SessionActionRequest("Review", alert.AbuseAlertId));

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("WAITING", session.Status);
        Assert.Equal("OPEN", alert.Status);
    }

    [Fact]
    public async Task DoesNotChangeSessionOrAlertWhenRedisRevocationFails()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        var alert = await context.SeedAlertAsync(
            1, "HIGH", "OPEN", DateTime.UtcNow, eventId: 12, userId: 7, sessionId: session.SessionId,
            alertType: "TOKEN_REPLAY");
        context.Redis.Setup(redis => redis.RevokeAsync(
                12, 7, session.SessionId, "TKN1", CycleKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Controller.Block(session.SessionId, new SessionActionRequest("Review", alert.AbuseAlertId)));

        Assert.Equal("Redis unavailable", exception.Message);
        Assert.Equal("WAITING", session.Status);
        Assert.Equal("OPEN", alert.Status);
    }

    private static string? GetStatus(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return ok.Value!.GetType().GetProperty("status")!.GetValue(ok.Value) as string;
    }
}
