global using Moq;

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using QueueAPI.Data;
using QueueAPI.Models;
using QueueAPI.Services;
using QueueAPI.Time;

namespace QueueAPI.UnitTests;

internal sealed class QueueServiceTestContext : IDisposable
{
    private readonly HttpClient eventApiClient;

    public QueueDbContext Db { get; }
    public Mock<IRedisQueueStore> Redis { get; } = new();
    public Mock<IHttpClientFactory> HttpClientFactory { get; } = new();
    public Mock<ILogger<QueueService>> Logger { get; } = new();
    public QueueService Service { get; }

    public QueueServiceTestContext(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? eventApiHandler = null)
    {
        var options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new QueueDbContext(options);
        Redis.Setup(redis => redis.RecordRegistrationRequestAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        Redis.Setup(redis => redis.RecordRepeatedRegistrationAttemptAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());
        Redis.Setup(redis => redis.RecordRevokedTokenReuseAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RevokedTokenReplaySnapshot?)null);

        eventApiHandler ??= (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        eventApiClient = new HttpClient(new CallbackMessageHandler(eventApiHandler))
        {
            BaseAddress = new Uri("http://event-api/")
        };
        HttpClientFactory.Setup(factory => factory.CreateClient("EventAPI")).Returns(eventApiClient);

        Service = new QueueService(Db, Redis.Object, HttpClientFactory.Object, Logger.Object);
    }

    public async Task<QueueConfiguration> SeedConfigurationAsync(
        int eventId = 12,
        DateTime? start = null,
        DateTime? end = null,
        int admissionRate = 100,
        bool queueEnabled = true,
        string fairnessPolicy = "RANDOMIZED")
    {
        var configuration = new QueueConfiguration
        {
            EventId = eventId,
            PreQueueStartTime = start ?? QueueTimeZone.Now.AddHours(-1),
            PreQueueEndTime = end ?? QueueTimeZone.Now.AddHours(1),
            AdmissionRate = admissionRate,
            QueueEnabled = queueEnabled,
            FairnessPolicy = fairnessPolicy
        };
        Db.QueueConfigurations.Add(configuration);
        await Db.SaveChangesAsync();
        return configuration;
    }

    public async Task<QueueSession> SeedSessionAsync(
        int eventId = 12,
        int userId = 7,
        string status = "REGISTERED",
        string token = "T-VALID",
        int? position = null,
        DateTime? createdAt = null)
    {
        var session = CreateSession(eventId, userId, status, token, position, createdAt);
        Db.QueueSessions.Add(session);
        await Db.SaveChangesAsync();
        return session;
    }

    public static QueueSession CreateSession(
        int eventId,
        int userId,
        string status,
        string token,
        int? position = null,
        DateTime? createdAt = null) =>
        new()
        {
            EventId = eventId,
            UserId = userId,
            SessionId = Guid.NewGuid().ToString("N"),
            QueueToken = token,
            Status = status,
            Position = position,
            CreatedAt = createdAt ?? QueueTimeZone.Now,
            UpdatedAt = createdAt ?? QueueTimeZone.Now
        };

    public void Dispose()
    {
        eventApiClient.Dispose();
        Db.Dispose();
    }

    private sealed class CallbackMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            callback(request, cancellationToken);
    }
}

internal static class QueueServiceTestAssertions
{
    internal static void VerifyErrorLogged(Mock<ILogger<QueueService>> logger, string messageFragment)
    {
        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(messageFragment)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
