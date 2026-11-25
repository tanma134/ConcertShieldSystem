using Microsoft.AspNetCore.Mvc;
using QueueAPI.DTOs;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class RevokeQueueTokenTests
{
    private const string CycleKey = "20261110100000000";

    [Fact]
    public async Task UTCID01_RevokesWaitingQueueTokenAndUpdatesSession()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedConfigurationAsync();
        var oldUpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var session = await context.SeedSessionAsync(updatedAt: oldUpdatedAt);
        context.Redis.Setup(redis => redis.RevokeAsync(
                12, 7, "SESS1", "TKN1", CycleKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await context.Controller.Revoke("TKN1", new RevokeQueueRequest("Bot activity"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal("Queue token revoked successfully.", ok.Value!.GetType().GetProperty("message")!.GetValue(ok.Value));
        Assert.Equal("REVOKED", session.Status);
        Assert.True(session.UpdatedAt > oldUpdatedAt);
        context.Redis.Verify(redis => redis.RevokeAsync(
            12, 7, "SESS1", "TKN1", CycleKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_RevokesQueueTokenWhenReasonIsNull()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.RevokeAsync(
                12, 7, "SESS1", "TKN1", CycleKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await context.Controller.Revoke("TKN1", new RevokeQueueRequest(null));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("REVOKED", session.Status);
        context.Redis.Verify(redis => redis.RevokeAsync(
            12, 7, "SESS1", "TKN1", CycleKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID03_AllowsRepeatedRevocationOfAlreadyRevokedToken()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync(status: "REVOKED");
        context.Redis.Setup(redis => redis.RevokeAsync(
                12, 7, "SESS1", "TKN1", CycleKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await context.Controller.Revoke("TKN1", new RevokeQueueRequest("Bot activity"));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("REVOKED", session.Status);
        context.Redis.Verify(redis => redis.RevokeAsync(
            12, 7, "SESS1", "TKN1", CycleKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID04_ReturnsNotFoundForUnknownToken()
    {
        using var context = new QueueAdminTestContext();

        var result = await context.Controller.Revoke("UNKNOWN", new RevokeQueueRequest("Bot activity"));

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        Assert.Equal("TOKEN_INVALID", notFound.Value!.GetType().GetProperty("code")!.GetValue(notFound.Value));
        context.Redis.VerifyNoOtherCalls();
        Assert.Empty(context.Db.QueueSessions);
    }

    [Fact]
    public async Task UTCID05_ReturnsNotFoundWhenSessionEventHasNoConfiguration()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedSessionAsync();

        var result = await context.Controller.Revoke("TKN1", new RevokeQueueRequest("Bot activity"));

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("EVENT_NOT_FOUND", notFound.Value!.GetType().GetProperty("code")!.GetValue(notFound.Value));
        Assert.Equal("WAITING", Assert.Single(context.Db.QueueSessions).Status);
        context.Redis.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID06_PropagatesRedisFailureWithoutChangingSession()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.RevokeAsync(
                12, 7, "SESS1", "TKN1", CycleKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Controller.Revoke("TKN1", new RevokeQueueRequest("Bot activity")));

        Assert.Equal("Redis unavailable", exception.Message);
        Assert.Equal("WAITING", session.Status);
    }
}
