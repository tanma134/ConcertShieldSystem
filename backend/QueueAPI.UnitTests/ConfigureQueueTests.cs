using System.Net;
using QueueAPI.DTOs;
using QueueAPI.Models;
using QueueAPI.Services;
using QueueAPI.Time;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class ConfigureQueueTests
{
    [Fact]
    public async Task UTCID01_InsertsConfigurationForValidLocalTimes()
    {
        using var context = new QueueServiceTestContext();
        var start = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified);

        var result = await context.Service.ConfigureAsync(12, new ConfigureQueueRequest(start, end, 100), 7);

        var configuration = Assert.Single(context.Db.QueueConfigurations);
        Assert.Equal(12, result.EventId);
        Assert.Equal(start, configuration.PreQueueStartTime);
        Assert.Equal(end, configuration.PreQueueEndTime);
        Assert.Equal(100, configuration.AdmissionRate);
        Assert.Equal("RANDOMIZED", configuration.FairnessPolicy);
    }

    [Fact]
    public async Task UTCID02_UpdatesExistingConfigurationInsteadOfInsertingAnother()
    {
        using var context = new QueueServiceTestContext();
        var previous = await context.SeedConfigurationAsync(admissionRate: 25);
        var updateStartedAt = QueueTimeZone.Now;
        var start = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified);

        var result = await context.Service.ConfigureAsync(12, new ConfigureQueueRequest(start, end, 200), 7);
        var updateFinishedAt = QueueTimeZone.Now;

        var configuration = Assert.Single(context.Db.QueueConfigurations);
        Assert.Equal(previous.QueueConfigurationId, configuration.QueueConfigurationId);
        Assert.Equal(200, configuration.AdmissionRate);
        Assert.Equal(start, configuration.PreQueueStartTime);
        Assert.Equal(end, configuration.PreQueueEndTime);
        Assert.Equal(200, result.AdmissionRate);
        Assert.InRange(configuration.UpdatedAt, updateStartedAt, updateFinishedAt);
    }

    [Fact]
    public async Task UTCID03_ConvertsUtcInputToVietnamLocalTime()
    {
        using var context = new QueueServiceTestContext();
        var startUtc = new DateTime(2026, 11, 10, 2, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc);

        var result = await context.Service.ConfigureAsync(12, new ConfigureQueueRequest(startUtc, endUtc, 100), 7);

        var configuration = Assert.Single(context.Db.QueueConfigurations);
        var expectedStart = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);
        var expectedEnd = new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(expectedStart, configuration.PreQueueStartTime);
        Assert.Equal(expectedEnd, configuration.PreQueueEndTime);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.FromHours(7)), result.PreQueueStartTime);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.FromHours(7)), result.PreQueueEndTime);
    }

    [Fact]
    public async Task UTCID04_AcceptsOneSecondPreQueueWindow()
    {
        using var context = new QueueServiceTestContext();
        var start = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddSeconds(1);

        var result = await context.Service.ConfigureAsync(12, new ConfigureQueueRequest(start, end, 100), 7);

        Assert.Equal(start.AddSeconds(1), result.PreQueueEndTime.DateTime);
        Assert.Single(context.Db.QueueConfigurations);
    }

    [Fact]
    public async Task UTCID05_RejectsEqualPreQueueTimes()
    {
        using var context = new QueueServiceTestContext();
        var time = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            context.Service.ConfigureAsync(12, new ConfigureQueueRequest(time, time, 100), 7));

        Assert.Contains("start time must be before end time", exception.Message);
        Assert.Empty(context.Db.QueueConfigurations);
    }

    [Fact]
    public async Task UTCID06_RejectsStartAfterEnd()
    {
        using var context = new QueueServiceTestContext();
        var start = new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddMinutes(-1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.Service.ConfigureAsync(12, new ConfigureQueueRequest(start, end, 100), 7));

        Assert.Empty(context.Db.QueueConfigurations);
    }

    [Fact]
    public async Task UTCID07_ThrowsNotFoundWhenEventApiReturns404()
    {
        using var context = new QueueServiceTestContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var start = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            context.Service.ConfigureAsync(12, new ConfigureQueueRequest(start, start.AddHours(1), 100), 7));

        Assert.Equal("EVENT_NOT_FOUND", exception.Message);
        Assert.Empty(context.Db.QueueConfigurations);
    }

    [Fact]
    public async Task UTCID08_ThrowsUnavailableWhenEventApiReturnsServerError()
    {
        using var context = new QueueServiceTestContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var start = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);

        await Assert.ThrowsAsync<EventApiUnavailableException>(() =>
            context.Service.ConfigureAsync(12, new ConfigureQueueRequest(start, start.AddHours(1), 100), 7));

        Assert.Empty(context.Db.QueueConfigurations);
    }

    [Fact]
    public async Task UTCID09_LogsAndWrapsHttpRequestException()
    {
        using var context = new QueueServiceTestContext((_, _) => throw new HttpRequestException("network failure"));
        var start = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);

        await Assert.ThrowsAsync<EventApiUnavailableException>(() =>
            context.Service.ConfigureAsync(12, new ConfigureQueueRequest(start, start.AddHours(1), 100), 7));

        QueueServiceTestAssertions.VerifyErrorLogged(context.Logger, "EventAPI validation failed");
        Assert.Empty(context.Db.QueueConfigurations);
    }

    [Fact]
    public async Task UTCID10_LogsAndWrapsEventApiTimeout()
    {
        using var context = new QueueServiceTestContext((_, _) => throw new TaskCanceledException("request timed out"));
        var start = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);

        await Assert.ThrowsAsync<EventApiUnavailableException>(() =>
            context.Service.ConfigureAsync(12, new ConfigureQueueRequest(start, start.AddHours(1), 100), 7));

        QueueServiceTestAssertions.VerifyErrorLogged(context.Logger, "EventAPI validation timed out");
        Assert.Empty(context.Db.QueueConfigurations);
    }
}
