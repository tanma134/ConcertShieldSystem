using Microsoft.AspNetCore.Mvc;
using QueueAPI.DTOs;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class ViewAbuseAlertDetailsTests
{
    [Fact]
    public async Task UTCID01_ReturnsAlertDetailsWithSafeFingerprintAndVietnamOffset()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedAlertAsync(
            5,
            "MEDIUM",
            "OPEN",
            new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified),
            queueToken: "ABCDEF1234567890",
            alertType: "AUTOMATED_REQUEST_PATTERN",
            detectedBehavior: "Too many requests");

        var result = await context.Controller.Alert(5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var alert = Assert.IsType<AbuseAlertResponse>(ok.Value);
        Assert.Equal(5, alert.AlertId);
        Assert.Equal(12, alert.EventId);
        Assert.Equal(7, alert.UserId);
        Assert.Equal("SESS1", alert.SessionId);
        Assert.Equal(QueueAPI.Services.QueueAbuseDetection.TokenFingerprint("ABCDEF1234567890"), alert.TokenFingerprint);
        Assert.Equal("AUTOMATED_REQUEST_PATTERN", alert.AlertType);
        Assert.Equal("MEDIUM", alert.Severity);
        Assert.Equal("OPEN", alert.Status);
        Assert.Equal("Too many requests", alert.DetectedBehavior);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.FromHours(7)), alert.CreatedAt);
    }

    [Fact]
    public async Task UTCID02_ReturnsNotFoundForUnknownAlertId()
    {
        using var context = new QueueAdminTestContext();

        var result = await context.Controller.Alert(999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UTCID03_HashesAnyLegacyValueBeforeReturningIt()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedAlertAsync(
            6,
            "HIGH",
            "OPEN",
            new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified),
            queueToken: "abc");

        var result = await context.Controller.Alert(6);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var alert = Assert.IsType<AbuseAlertResponse>(ok.Value);
        Assert.Equal(QueueAPI.Services.QueueAbuseDetection.TokenFingerprint("abc"), alert.TokenFingerprint);
        Assert.DoesNotContain("abc", alert.TokenFingerprint);
    }
}
