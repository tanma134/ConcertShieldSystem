using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.UnitTests;

public class CancelHoldSessionServiceTests
{
    private const string HoldId = "hold-1";
    private const int UserId = 7;
    private const int EventId = 42;

    [Fact]
    public async Task C01_CancelsHoldWithTicketsAndSeats_ReleasesAllResources()
    {
        var database = new Mock<IDatabase>();
        SetupScriptEvaluation(database);

        SetupSession(database, new HoldSession
        {
            HoldId = HoldId,
            UserId = UserId,
            EventId = EventId,
            Tickets = new List<TicketHoldItem>
            {
                new() { TicketTypeId = 3, Quantity = 2 }
            },
            SeatIds = new List<int> { 101, 102 }
        });

        var service = CreateService(database);

        await service.CancelHoldSessionAsync(HoldId, UserId);

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 4 &&
                keys[0].ToString() == "hold:ticket:3:user:7"),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 1 &&
                keys[0].ToString() == "hold:seat:42:101"),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 1 &&
                keys[0].ToString() == "hold:seat:42:102"),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        VerifySessionAndUserLockAreDeleted(database);
    }

    [Fact]
    public async Task C02_CancelsGeneralAdmissionHold_ReleasesTickets()
    {
        var database = new Mock<IDatabase>();
        SetupScriptEvaluation(database);

        SetupSession(database, new HoldSession
        {
            HoldId = HoldId,
            UserId = UserId,
            EventId = EventId,
            Tickets = new List<TicketHoldItem>
            {
                new() { TicketTypeId = 5, Quantity = 3 }
            },
            SeatIds = new List<int>()
        });

        var service = CreateService(database);

        await service.CancelHoldSessionAsync(HoldId, UserId);

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 4 &&
                keys[0].ToString() == "hold:ticket:5:user:7"),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        VerifySessionAndUserLockAreDeleted(database);
    }

    [Fact]
    public async Task C03_CancelsReservedSeatHold_ReleasesSeats()
    {
        var database = new Mock<IDatabase>();
        SetupScriptEvaluation(database);

        SetupSession(database, new HoldSession
        {
            HoldId = HoldId,
            UserId = UserId,
            EventId = EventId,
            Tickets = new List<TicketHoldItem>(),
            SeatIds = new List<int> { 103, 104 }
        });

        var service = CreateService(database);

        await service.CancelHoldSessionAsync(HoldId, UserId);

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 1 &&
                keys[0].ToString() == "hold:seat:42:103"),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 1 &&
                keys[0].ToString() == "hold:seat:42:104"),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        VerifySessionAndUserLockAreDeleted(database);
    }

    [Fact]
    public async Task C04_CancelsHoldWithNoTicketsOrSeats_DeletesSessionAndUserLock()
    {
        var database = new Mock<IDatabase>();
        SetupScriptEvaluation(database);

        SetupSession(database, new HoldSession
        {
            HoldId = HoldId,
            UserId = UserId,
            EventId = EventId,
            Tickets = new List<TicketHoldItem>(),
            SeatIds = new List<int>()
        });

        var service = CreateService(database);

        await service.CancelHoldSessionAsync(HoldId, UserId);

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

        VerifySessionAndUserLockAreDeleted(database);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task C05_InvalidHoldIdIsRejected(string? holdId)
    {
        var database = new Mock<IDatabase>();
        var service = CreateService(database);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CancelHoldSessionAsync(holdId!, UserId));

        VerifyRedisWasNotAccessed(database);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task C06_NonPositiveUserIdIsRejected(int userId)
    {
        var database = new Mock<IDatabase>();
        var service = CreateService(database);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CancelHoldSessionAsync(HoldId, userId));

        VerifyRedisWasNotAccessed(database);
    }

    [Fact]
    public async Task C07_MissingHoldSessionIsRejected()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(key =>
                    key.ToString() == "hold:session:hold-1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var service = CreateService(database);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CancelHoldSessionAsync(HoldId, UserId));

        VerifyNoCancellationSideEffects(database);
    }

    [Fact]
    public async Task C08_NullHoldSessionDataIsRejected()
    {
        var database = new Mock<IDatabase>();
        SetupSessionJson(database, "null");

        var service = CreateService(database);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CancelHoldSessionAsync(HoldId, UserId));

        VerifyNoCancellationSideEffects(database);
    }

    [Fact]
    public async Task C09_MalformedHoldSessionJsonIsRejected()
    {
        var database = new Mock<IDatabase>();
        SetupSessionJson(database, "not-json");

        var service = CreateService(database);

        await Assert.ThrowsAsync<JsonException>(
            () => service.CancelHoldSessionAsync(HoldId, UserId));

        VerifyNoCancellationSideEffects(database);
    }

    [Fact]
    public async Task C10_HoldOwnedByAnotherUserIsRejected()
    {
        var database = new Mock<IDatabase>();
        SetupSession(database, new HoldSession
        {
            HoldId = HoldId,
            UserId = 99,
            EventId = EventId,
            Tickets = new List<TicketHoldItem>
            {
                new() { TicketTypeId = 3, Quantity = 1 }
            },
            SeatIds = new List<int> { 101 }
        });

        var service = CreateService(database);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CancelHoldSessionAsync(HoldId, UserId));

        VerifyNoCancellationSideEffects(database);
    }

    private static HoldService CreateService(Mock<IDatabase> database)
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(connection => connection.GetDatabase(
                It.IsAny<int>(),
                It.IsAny<object?>()))
            .Returns(database.Object);

        var eligibilityClient = new EventEligibilityClient(
            new HttpClient(new SuccessfulEligibilityHandler())
            {
                BaseAddress = new Uri("https://test.local/")
            });

        return new HoldService(
            redis.Object,
            NullLogger<HoldService>.Instance,
            Mock.Of<IEventApiClient>(),
            eligibilityClient);
    }

    private static void SetupSession(
        Mock<IDatabase> database,
        HoldSession session)
    {
        var json = JsonSerializer.Serialize(
            session,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        SetupSessionJson(database, json);
    }

    private static void SetupSessionJson(
        Mock<IDatabase> database,
        string json)
    {
        database
            .Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(key =>
                    key.ToString() == "hold:session:hold-1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);
    }

    private static void SetupScriptEvaluation(Mock<IDatabase> database)
    {
        database
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1L));

        database
            .Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }

    private static void VerifySessionAndUserLockAreDeleted(
        Mock<IDatabase> database)
    {
        database.Verify(db => db.KeyDeleteAsync(
            It.Is<RedisKey>(key =>
                key.ToString() == "hold:session:hold-1"),
            It.IsAny<CommandFlags>()),
            Times.Once);

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 1 &&
                keys[0].ToString() == "hold:user:7:active"),
            It.Is<RedisValue[]>(values =>
                values.Length == 1 &&
                values[0].ToString() == "hold-1"),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    private static void VerifyNoCancellationSideEffects(
        Mock<IDatabase> database)
    {
        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()),
            Times.Never);

        database.Verify(db => db.KeyDeleteAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()),
            Times.Never);
    }

    private static void VerifyRedisWasNotAccessed(Mock<IDatabase> database)
    {
        database.Verify(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()),
            Times.Never);

        VerifyNoCancellationSideEffects(database);
    }

    private sealed class SuccessfulEligibilityHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var eligibility = new EventEligibility(
                CanSell: true,
                Reason: "",
                Status: "Published",
                StartsAt: DateTime.UtcNow.AddDays(-1),
                EndsAt: DateTime.UtcNow.AddDays(1),
                ScheduleVersion: 1,
                RequiresVirtualQueue: false);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(eligibility)
            });
        }
    }
}