using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingAPI.DTOS;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.Tests;

public class ChangeSeatSelectionHoldServiceTests
{
    [Fact]
    public async Task C01_ChangesSeatAndUpdatesAttendeeAssignment()
    {
        var session = CreateSession(seatIds: new List<int> { 101 });
        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, TimeSpan.FromMinutes(5));

        database
            .SetupSequence(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(true) // Giữ được ghế mới
            .ReturnsAsync(true); // Lưu session đã cập nhật

        SetupScriptEvaluation(database);

        var service = CreateService(database);

        var result = await service.ChangeSeatsAsync(
            "hold-1",
            userId: 7,
            seatIds: new List<int> { 102 });

        Assert.Equal(new[] { 102 }, result.SeatIds);
        Assert.Equal(102, Assert.Single(result.Attendees).SeatId);

        VerifySeatSet(database, 102, Times.Once());
        VerifySessionSaved(database, Times.Once());

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 1 &&
                keys[0].ToString() == "hold:seat:42:101"),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once());
    }

    [Fact]
    public async Task C02_KeepingTheSameSeatDoesNotAcquireOrReleaseASeat()
    {
        var session = CreateSession(seatIds: new List<int> { 101 });
        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, TimeSpan.FromMinutes(5));

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(true);

        SetupScriptEvaluation(database);

        var service = CreateService(database);

        var result = await service.ChangeSeatsAsync(
            "hold-1",
            userId: 7,
            seatIds: new List<int> { 101 });

        Assert.Equal(new[] { 101 }, result.SeatIds);
        Assert.Equal(101, Assert.Single(result.Attendees).SeatId);

        VerifySeatSet(database, 101, Times.Never());
        VerifySessionSaved(database, Times.Once());

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Never());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task C03_EmptyHoldIdIsRejected(string holdId)
    {
        var service = CreateService(new Mock<IDatabase>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ChangeSeatsAsync(
                holdId,
                userId: 7,
                seatIds: new List<int> { 102 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task C04_NonPositiveUserIdIsRejected(int userId)
    {
        var service = CreateService(new Mock<IDatabase>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId,
                seatIds: new List<int> { 102 }));
    }

    [Fact]
    public async Task C05_EmptySeatListIsRejected()
    {
        var service = CreateService(new Mock<IDatabase>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task C06_NonPositiveSeatIdIsRejected(int seatId)
    {
        var service = CreateService(new Mock<IDatabase>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { seatId }));
    }

    [Fact]
    public async Task C07_DuplicateSeatIdsAreRejected()
    {
        var service = CreateService(new Mock<IDatabase>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 102, 102 }));
    }

    [Fact]
    public async Task C08_MissingHoldSessionIsRejected()
    {
        var database = new Mock<IDatabase>();

        database
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var service = CreateService(database);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 102 }));
    }

    [Fact]
    public async Task C09_SessionOwnedByAnotherUserIsRejected()
    {
        var session = CreateSession(
            seatIds: new List<int> { 101 },
            userId: 8);

        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, TimeSpan.FromMinutes(5));

        var service = CreateService(database);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 102 }));
    }

    [Fact]
    public async Task C10_ExpiredSessionIsRejected()
    {
        var session = CreateSession(
            seatIds: new List<int> { 101 },
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, TimeSpan.FromMinutes(5));

        var service = CreateService(database);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 102 }));
    }

    [Fact]
    public async Task C11_SessionWithoutRedisTtlIsRejected()
    {
        var session = CreateSession(seatIds: new List<int> { 101 });
        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, null);

        var service = CreateService(database);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 102 }));
    }

    [Fact]
    public async Task C12_ChangingToDifferentNumberOfSeatsIsRejected()
    {
        var session = CreateSession(seatIds: new List<int> { 101 });
        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, TimeSpan.FromMinutes(5));

        var service = CreateService(database);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 102, 103 }));
    }

    [Fact]
    public async Task C13_ChangingSeatsWhenSalesAreClosedIsRejected()
    {
        var session = CreateSession(seatIds: new List<int> { 101 });
        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, TimeSpan.FromMinutes(5));

        var service = CreateService(database, canSell: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 102 }));

        VerifySeatSet(database, 102, Times.Never());
        VerifySessionSaved(database, Times.Never());
    }

    [Fact]
    public async Task C14_WhenNewSeatIsUnavailable_CurrentSeatRemainsHeld()
    {
        var session = CreateSession(
            seatIds: new List<int> { 101, 102 });

        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, TimeSpan.FromMinutes(5));

        database
            .Setup(db => db.StringSetAsync(
                It.Is<RedisKey>(key =>
                    key.ToString() == "hold:seat:42:103"),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(false);

        var service = CreateService(database);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 103, 104 }));

        VerifySessionSaved(database, Times.Never());

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Never());
    }

    [Fact]
    public async Task C15_WhenSecondNewSeatIsUnavailable_FirstNewSeatIsReleased()
    {
        var session = CreateSession(
            seatIds: new List<int> { 101, 102 });

        var database = CreateDatabaseWithSession(session);
        SetupTtl(database, TimeSpan.FromMinutes(5));

        database
            .SetupSequence(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(true)  // Ghế 103 được giữ
            .ReturnsAsync(false); // Ghế 104 không giữ được

        SetupScriptEvaluation(database);

        var service = CreateService(database);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeSeatsAsync(
                "hold-1",
                userId: 7,
                seatIds: new List<int> { 103, 104 }));

        database.Verify(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys =>
                keys.Length == 1 &&
                keys[0].ToString() == "hold:seat:42:103"),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.Once());

        VerifySessionSaved(database, Times.Never());
    }

    private static HoldSession CreateSession(
        List<int> seatIds,
        int userId = 7,
        DateTime? expiresAtUtc = null)
    {
        return new HoldSession
        {
            HoldId = "hold-1",
            UserId = userId,
            EventId = 42,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            DurationSeconds = 600,
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(5),
            SeatIds = seatIds,
            Tickets = new List<TicketHoldItem>
            {
                new() { TicketTypeId = 3, Quantity = seatIds.Count }
            },
            Attendees = seatIds.Select(seatId => new HoldAttendeeDto
            {
                FullName = "Test User",
                TicketTypeId = 3,
                SeatId = seatId
            }).ToList()
        };
    }

    private static Mock<IDatabase> CreateDatabaseWithSession(HoldSession session)
    {
        var database = new Mock<IDatabase>();
        var json = JsonSerializer.Serialize(
            session,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        database
            .Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(key =>
                    key.ToString() == "hold:session:hold-1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        return database;
    }

    private static void SetupTtl(
        Mock<IDatabase> database,
        TimeSpan? ttl)
    {
        database
            .Setup(db => db.KeyTimeToLiveAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(ttl);
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
    }

    private static void VerifySeatSet(
        Mock<IDatabase> database,
        int seatId,
        Times times)
    {
        database.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(key =>
                key.ToString() == $"hold:seat:42:{seatId}"),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            When.NotExists), times);
    }

    private static void VerifySessionSaved(
        Mock<IDatabase> database,
        Times times)
    {
        database.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(key =>
                key.ToString() == "hold:session:hold-1"),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            When.Exists), times);
    }

    private static HoldService CreateService(
        Mock<IDatabase> database,
        bool canSell = true)
    {
        var redis = new Mock<IConnectionMultiplexer>();

        redis
            .Setup(connection => connection.GetDatabase(
                It.IsAny<int>(),
                It.IsAny<object?>()))
            .Returns(database.Object);

        var eventApi = new Mock<IEventApiClient>();

        eventApi
            .Setup(api => api.GetEventByIdAsync(42))
            .ReturnsAsync(new EventApiResponseDto { EventId = 42 });

        var eligibilityClient = new EventEligibilityClient(
            new HttpClient(new EligibilityHandler(canSell))
            {
                BaseAddress = new Uri("https://test.local/")
            });

        return new HoldService(
            redis.Object,
            NullLogger<HoldService>.Instance,
            eventApi.Object,
            eligibilityClient);
    }

    private sealed class EligibilityHandler(bool canSell) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var eligibility = new EventEligibility(
                CanSell: canSell,
                Reason: canSell ? "" : "Sales are closed",
                Status: canSell ? "Published" : "Closed",
                StartsAt: DateTime.UtcNow.AddDays(-1),
                EndsAt: DateTime.UtcNow.AddDays(1),
                ScheduleVersion: 1,
                RequiresVirtualQueue: false);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(eligibility)
            });
        }
    }
}