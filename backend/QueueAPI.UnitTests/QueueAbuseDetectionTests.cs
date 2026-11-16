using System.Text.Json;
using QueueAPI.Services;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class QueueAbuseDetectionTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task MultipleRegistrationAttemptsUseThePerEventRedisCounter(int attempts, bool expectedAlert)
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        var recorded = 0;
        context.Redis.Setup(redis => redis.RecordRepeatedRegistrationAttemptAsync(
                12, 7, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((int _, int _, long timestamp, CancellationToken _) =>
            {
                recorded++;
                return Task.FromResult<IReadOnlyList<long>>(
                    Enumerable.Range(0, recorded).Select(index => timestamp - (recorded - index - 1) * 1000L).ToArray());
            });
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, session.QueueToken, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        for (var index = 0; index < attempts; index++)
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.RegisterAsync(12, 7));

        var alerts = context.Db.AbuseAlerts.ToArray();
        Assert.Equal(expectedAlert ? 1 : 0, alerts.Length);
        if (expectedAlert)
        {
            Assert.Equal("MULTIPLE_REGISTRATION_ATTEMPTS", alerts[0].AlertType);
            Assert.Equal("LOW", alerts[0].Severity);
            Assert.Equal(session.SessionId, alerts[0].SessionId);
            Assert.DoesNotContain(session.QueueToken, alerts[0].TokenFingerprint);
        }
    }

    [Fact]
    public async Task RepeatedAttemptsAcrossEventsAreNotCombined()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync(12);
        await context.SeedConfigurationAsync(13);
        var first = await context.SeedSessionAsync(eventId: 12, userId: 7, token: "TOKEN-12");
        var second = await context.SeedSessionAsync(eventId: 13, userId: 7, token: "TOKEN-13");
        var attemptsByEvent = new Dictionary<int, int>();
        context.Redis.Setup(redis => redis.RecordRepeatedRegistrationAttemptAsync(
                It.IsAny<int>(), 7, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((int eventId, int _, long timestamp, CancellationToken _) =>
            {
                attemptsByEvent[eventId] = attemptsByEvent.GetValueOrDefault(eventId) + 1;
                var count = attemptsByEvent[eventId];
                return Task.FromResult<IReadOnlyList<long>>(
                    Enumerable.Range(0, count).Select(index => timestamp - (count - index - 1) * 1000L).ToArray());
            });
        context.Redis.Setup(redis => redis.RegisterAsync(
                It.IsAny<int>(), 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.RegisterAsync(12, 7));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.RegisterAsync(12, 7));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.RegisterAsync(13, 7));

        Assert.Empty(context.Db.AbuseAlerts);
        Assert.Equal("TOKEN-12", first.QueueToken);
        Assert.Equal("TOKEN-13", second.QueueToken);
    }

    [Fact]
    public async Task RepeatedRegistrationsDoNotCreateDuplicateAlertsForTheSameSession()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        var recorded = 0;
        context.Redis.Setup(redis => redis.RecordRepeatedRegistrationAttemptAsync(
                12, 7, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((int _, int _, long timestamp, CancellationToken _) =>
            {
                recorded++;
                return Task.FromResult<IReadOnlyList<long>>(
                    Enumerable.Range(0, recorded).Select(index => timestamp - (recorded - index - 1) * 1000L).ToArray());
            });
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, session.QueueToken, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        for (var index = 0; index < 5; index++)
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.RegisterAsync(12, 7));

        Assert.Single(context.Db.AbuseAlerts);
    }

    [Fact]
    public async Task TwentyNineRegistrationRequestsDoNotCreateAutomatedPatternAlert()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var requestCount = 0;
        SetupRequestTimestamps(context, (timestamp) =>
        {
            requestCount++;
            return EvenlySpaced(timestamp, requestCount);
        });
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        for (var index = 0; index < 29; index++)
            await RegisterOrIgnoreAlreadyRegisteredAsync(context);

        Assert.Empty(context.Db.AbuseAlerts);
    }

    [Fact]
    public async Task ThirtyRegularRegistrationRequestsCreateMediumAlertWithTimestampEvidence()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var requestCount = 0;
        SetupRequestTimestamps(context, timestamp =>
        {
            requestCount++;
            return EvenlySpaced(timestamp, requestCount);
        });
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        for (var index = 0; index < 30; index++)
            await RegisterOrIgnoreAlreadyRegisteredAsync(context);

        var alert = Assert.Single(context.Db.AbuseAlerts);
        Assert.Equal("AUTOMATED_REQUEST_PATTERN", alert.AlertType);
        Assert.Equal("MEDIUM", alert.Severity);
        using var evidence = JsonDocument.Parse(alert.DetectedBehavior);
        Assert.Equal(30, evidence.RootElement.GetProperty("requestCount").GetInt32());
        Assert.Equal(30, evidence.RootElement.GetProperty("requestTimestamps").GetArrayLength());
        Assert.True(evidence.RootElement.GetProperty("coefficientOfVariation").GetDouble() <= 0.1);
    }

    [Fact]
    public async Task IrregularRegistrationRequestsDoNotCreateAutomatedPatternAlert()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var requestCount = 0;
        SetupRequestTimestamps(context, timestamp =>
        {
            requestCount++;
            return Irregular(timestamp, requestCount);
        });
        context.Redis.Setup(redis => redis.RegisterAsync(
                12, 7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        for (var index = 0; index < 30; index++)
            await RegisterOrIgnoreAlreadyRegisteredAsync(context);

        Assert.Empty(context.Db.AbuseAlerts);
    }

    [Fact]
    public async Task QueueStatusPollingDoesNotCountAsRegistrationAbuse()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync();
        context.Redis.Setup(redis => redis.GetStatusAsync(
                12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0, "REGISTERED", (DateTimeOffset?)null));

        for (var index = 0; index < 30; index++)
            await context.Service.GetStatusAsync(12, 7, session.QueueToken);

        context.Redis.Verify(redis => redis.RecordRegistrationRequestAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(context.Db.AbuseAlerts);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(24, false)]
    [InlineData(25, true)]
    public async Task RevokedTokenReplayRequiresTwentyFiveAttempts(int attempts, bool expectedAlert)
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        const string token = "REVOKED-QUEUE-TOKEN";
        var session = await context.SeedSessionAsync(status: "REVOKED", token: token);
        var recorded = 0;
        context.Redis.Setup(redis => redis.RecordRevokedTokenReuseAsync(
                12, token, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((int _, string _, long timestamp, CancellationToken _) =>
            {
                recorded++;
                var timestamps = Enumerable.Range(0, Math.Min(recorded, attempts))
                    .Select(index => timestamp - (Math.Min(recorded, attempts) - index - 1) * 1000L)
                    .ToArray();
                return Task.FromResult<RevokedTokenReplaySnapshot?>(
                    new(7, session.SessionId, QueueAbuseDetection.TokenFingerprint(token), timestamps));
            });

        for (var index = 0; index < attempts; index++)
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.JoinAsync(12, 7, token));

        var alerts = context.Db.AbuseAlerts.ToArray();
        Assert.Equal(expectedAlert ? 1 : 0, alerts.Length);
        if (expectedAlert)
        {
            Assert.Equal("TOKEN_REPLAY", alerts[0].AlertType);
            Assert.Equal("HIGH", alerts[0].Severity);
            Assert.Equal(QueueAbuseDetection.TokenFingerprint(token), alerts[0].TokenFingerprint);
            Assert.DoesNotContain(token, alerts[0].TokenFingerprint);
            Assert.DoesNotContain(token, alerts[0].DetectedBehavior);
        }
    }

    [Fact]
    public async Task ExpiredTokenDoesNotCreateTokenReplayAlert()
    {
        using var context = new QueueServiceTestContext();
        await context.SeedConfigurationAsync();
        var session = await context.SeedSessionAsync(status: "EXPIRED", token: "EXPIRED-TOKEN");
        context.Redis.Setup(redis => redis.JoinAsync(
                12, 7, session.QueueToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, 0, 0, "TOKEN_INVALID"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.Service.JoinAsync(12, 7, session.QueueToken));

        context.Redis.Verify(redis => redis.RecordRevokedTokenReuseAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(context.Db.AbuseAlerts);
    }

    private static void SetupRequestTimestamps(
        QueueServiceTestContext context,
        Func<long, IReadOnlyList<long>> getTimestamps)
    {
        context.Redis.Setup(redis => redis.RecordRegistrationRequestAsync(
                12, 7, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((int _, int _, long timestamp, CancellationToken _) =>
                Task.FromResult(getTimestamps(timestamp)));
    }

    private static long[] EvenlySpaced(long now, int count) =>
        Enumerable.Range(0, count)
            .Select(index => now - (count - index - 1) * 2000L)
            .ToArray();

    private static long[] Irregular(long now, int count)
    {
        var timestamps = new long[count];
        var cursor = now - (count - 1) * 2000L;
        timestamps[0] = cursor;
        for (var index = 1; index < count; index++)
        {
            cursor += index % 2 == 0 ? 500 : 3500;
            timestamps[index] = cursor;
        }
        var shift = now - timestamps[^1];
        return timestamps.Select(timestamp => timestamp + shift).ToArray();
    }

    private static async Task RegisterOrIgnoreAlreadyRegisteredAsync(QueueServiceTestContext context)
    {
        try
        {
            await context.Service.RegisterAsync(12, 7);
        }
        catch (InvalidOperationException exception) when (exception.Message == "ALREADY_REGISTERED")
        {
        }
    }
}
