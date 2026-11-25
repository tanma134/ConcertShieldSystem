using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QueueAPI.Controllers;
using QueueAPI.DTOs;
using QueueAPI.Models;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class ViewAbuseAlertListTests
{
    [Fact]
    public void AdminControllerKeepsQueueAdminAuthorizationPolicy()
    {
        var authorize = typeof(QueueAdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("QueueAdmin", authorize.Policy);
    }

    [Fact]
    public async Task UTCID01_ReturnsSupportedAlertsNewestFirstWithSafeFingerprint()
    {
        using var context = new QueueAdminTestContext();
        await SeedThreeAlertsAsync(context);

        var alerts = await GetAlertsAsync(context);

        Assert.Collection(
            alerts,
            alert =>
            {
                Assert.Equal(3, alert.AlertId);
                Assert.Equal(QueueAPI.Services.QueueAbuseDetection.TokenFingerprint("TOKEN0003"), alert.TokenFingerprint);
            },
            alert => Assert.Equal(2, alert.AlertId),
            alert => Assert.Equal(1, alert.AlertId));
    }

    [Fact]
    public async Task UTCID02_FiltersBySeverityCaseInsensitively()
    {
        using var context = new QueueAdminTestContext();
        await SeedThreeAlertsAsync(context);

        var alerts = await GetAlertsAsync(context, severity: "high");

        Assert.Equal(new long[] { 3, 1 }, alerts.Select(alert => alert.AlertId));
    }

    [Fact]
    public async Task UTCID03_FiltersByStatusCaseInsensitively()
    {
        using var context = new QueueAdminTestContext();
        await SeedThreeAlertsAsync(context);

        var alerts = await GetAlertsAsync(context, status: "open");

        Assert.Equal(new long[] { 2, 1 }, alerts.Select(alert => alert.AlertId));
    }

    [Fact]
    public async Task UTCID04_AppliesSeverityAndStatusFiltersTogether()
    {
        using var context = new QueueAdminTestContext();
        await SeedThreeAlertsAsync(context);

        var alerts = await GetAlertsAsync(context, severity: "high", status: "open");

        var alert = Assert.Single(alerts);
        Assert.Equal(1, alert.AlertId);
    }

    [Fact]
    public async Task UTCID05_ReturnsOnlyMediumSeverityAlerts()
    {
        using var context = new QueueAdminTestContext();
        await SeedThreeAlertsAsync(context);

        var alert = Assert.Single(await GetAlertsAsync(context, severity: "MEDIUM"));

        Assert.Equal(2, alert.AlertId);
    }

    [Fact]
    public async Task UTCID06_IgnoresWhitespaceSeverityAndEmptyStatusFilters()
    {
        using var context = new QueueAdminTestContext();
        await SeedThreeAlertsAsync(context);

        var alerts = await GetAlertsAsync(context, severity: "   ", status: "");

        Assert.Equal(new long[] { 3, 2, 1 }, alerts.Select(alert => alert.AlertId));
    }

    [Fact]
    public async Task UTCID07_ReturnsEmptyListForUnknownSeverity()
    {
        using var context = new QueueAdminTestContext();
        await SeedThreeAlertsAsync(context);

        var alerts = await GetAlertsAsync(context, severity: "UNKNOWN");

        Assert.Empty(alerts);
    }

    [Fact]
    public async Task UTCID09_HidesLegacyAlertTypesAndUnsupportedSeverities()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedAlertAsync(1, "HIGH", "OPEN", DateTime.UtcNow, alertType: "EVENT_SESSION_SPIKE");
        await context.SeedAlertAsync(2, "CRITICAL", "OPEN", DateTime.UtcNow, alertType: "TOKEN_REPLAY");

        Assert.Empty(await GetAlertsAsync(context));
        Assert.IsType<NotFoundResult>((await context.Controller.Alert(2)).Result);
    }

    [Fact]
    public async Task UTCID08_ReturnsEmptyListWhenNoAlertsExist()
    {
        using var context = new QueueAdminTestContext();

        var alerts = await GetAlertsAsync(context);

        Assert.Empty(alerts);
    }

    [Fact]
    public async Task UTCID09_LimitsResultsToFiveHundredNewestAlerts()
    {
        using var context = new QueueAdminTestContext();
        var origin = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var alerts = Enumerable.Range(1, 502)
            .Select(index => new AbuseAlert
            {
                AbuseAlertId = index,
                EventId = 12,
                UserId = index,
                SessionId = $"SESSION{index:D4}",
                TokenFingerprint = $"TOKEN{index:D6}",
                AlertType = "MULTIPLE_REGISTRATION_ATTEMPTS",
                Severity = "HIGH",
                Status = "OPEN",
                DetectedBehavior = "Test alert",
                CreatedAt = origin.AddMinutes(index)
            })
            .ToArray();
        context.Db.AbuseAlerts.AddRange(alerts);
        await context.Db.SaveChangesAsync();

        var result = await GetAlertsAsync(context);

        Assert.Equal(500, result.Length);
        Assert.Equal(502, result[0].AlertId);
        Assert.Equal(3, result[^1].AlertId);
    }

    [Fact]
    public async Task UTCID10_ReturnsFingerprintInsteadOfLegacyToken()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedAlertAsync(
            1, "HIGH", "OPEN",
            new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified),
            queueToken: "abc");

        var alerts = await GetAlertsAsync(context);
        var alert = Assert.Single(alerts);
        Assert.Equal(QueueAPI.Services.QueueAbuseDetection.TokenFingerprint("abc"), alert.TokenFingerprint);
        Assert.DoesNotContain("abc", alert.TokenFingerprint);
    }

    private static async Task SeedThreeAlertsAsync(QueueAdminTestContext context)
    {
        await context.SeedAlertAsync(
            1, "HIGH", "OPEN",
            new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified),
            queueToken: "TOKEN0001");
        await context.SeedAlertAsync(
            2, "MEDIUM", "OPEN",
            new DateTime(2026, 11, 10, 11, 0, 0, DateTimeKind.Unspecified),
            queueToken: "TOKEN0002");
        await context.SeedAlertAsync(
            3, "HIGH", "CLOSED",
            new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Unspecified),
            queueToken: "TOKEN0003");
    }

    private static async Task<AbuseAlertResponse[]> GetAlertsAsync(
        QueueAdminTestContext context,
        string? severity = null,
        string? status = null)
    {
        var result = await context.Controller.Alerts(severity, status);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<IEnumerable<AbuseAlertResponse>>(ok.Value).ToArray();
    }
}
