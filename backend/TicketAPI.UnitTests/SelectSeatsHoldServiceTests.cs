using System.Net;
using System.Net.Http.Json;
using BookingAPI.DTOS;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.Tests;

public class SelectSeatsHoldServiceTests
{
    [Fact]
    public async Task S01_SelectedSeatsAreHeldAndReturnedInSession()
    {
        var database = CreateDatabaseMock(returnValue: true);
        var eventApi = CreateEventApiMock();
        var service = CreateService(database, eventApi);

        var result = await service.CreateHoldSessionAsync(
            userId: 7,
            request: CreateRequest(101, 102));

        Assert.Equal(42, result.EventId);
        Assert.Equal(new[] { 101, 102 }, result.SeatIds);

        VerifyStringSetCalled(database, "hold:seat:42:101", Times.Once());
        VerifyStringSetCalled(database, "hold:seat:42:102", Times.Once());
    }

    [Fact]
    public async Task S02_NoSelectedSeatsOrTicketsIsRejected()
    {
        var database = CreateDatabaseMock(returnValue: true);
        var service = CreateService(database, CreateEventApiMock());

        var request = new CreateHoldSessionRequest
        {
            EventId = 42
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateHoldSessionAsync(7, request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task S03_NonPositiveSeatIdIsRejected(int seatId)
    {
        var database = CreateDatabaseMock(returnValue: true);
        var service = CreateService(database, CreateEventApiMock());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateHoldSessionAsync(7, CreateRequest(seatId)));
    }

    [Fact]
    public async Task S04_DuplicateSeatIdsAreRejected()
    {
        var database = CreateDatabaseMock(returnValue: true);
        var service = CreateService(database, CreateEventApiMock());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateHoldSessionAsync(7, CreateRequest(101, 101)));
    }

    [Fact]
    public async Task S05_ExistingActiveHoldSessionIsRejected()
    {
        var database = CreateDatabaseMock(returnValue: false);
        var service = CreateService(database, CreateEventApiMock());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateHoldSessionAsync(7, CreateRequest(101)));

        VerifyStringSetCalled(database, "hold:seat:42:101", Times.Never());
    }

    [Fact]
    public async Task S06_EventThatCannotBeSoldIsRejected()
    {
        var database = CreateDatabaseMock(returnValue: true);
        var eventApi = CreateEventApiMock();
        var service = CreateService(database, eventApi, canSell: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateHoldSessionAsync(7, CreateRequest(101)));

        database.Verify(
            db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()),
            Times.Never());

        eventApi.Verify(api => api.GetEventByIdAsync(42), Times.Never());
    }

    [Fact]
    public async Task S07_EventNotFoundIsRejected()
    {
        var database = CreateDatabaseMock(returnValue: true);
        var eventApi = new Mock<IEventApiClient>();

        eventApi
            .Setup(api => api.GetEventByIdAsync(42))
            .ReturnsAsync((EventApiResponseDto?)null);

        var service = CreateService(database, eventApi);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateHoldSessionAsync(7, CreateRequest(101)));

        database.Verify(
            db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()),
            Times.Never());
    }

    [Fact]
    public async Task S08_SeatAlreadyHeldByAnotherUserIsRejected()
    {
        var database = new Mock<IDatabase>();

        database
            .SetupSequence(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(true)  // Lấy được khóa phiên của user
            .ReturnsAsync(false); // Không lấy được ghế

        SetupScriptEvaluation(database);

        var service = CreateService(database, CreateEventApiMock());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateHoldSessionAsync(7, CreateRequest(101)));
    }

    [Fact]
    public async Task S09_WhenOneOfSeveralSeatsCannotBeHeld_AlreadyHeldSeatsAreReleased()
    {
        var database = new Mock<IDatabase>();

        database
            .SetupSequence(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(true)  // Lấy được khóa phiên của user
            .ReturnsAsync(true)  // Giữ được ghế 101
            .ReturnsAsync(false); // Ghế 102 đã bị giữ

        SetupScriptEvaluation(database);

        var service = CreateService(database, CreateEventApiMock());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateHoldSessionAsync(7, CreateRequest(101, 102)));

        database.Verify(
            db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.AtLeastOnce());
    }

    private static CreateHoldSessionRequest CreateRequest(params int[] seatIds)
    {
        return new CreateHoldSessionRequest
        {
            EventId = 42,
            SeatIds = seatIds.ToList()
        };
    }

    private static Mock<IDatabase> CreateDatabaseMock(bool returnValue)
    {
        var database = new Mock<IDatabase>();

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>()))
            .ReturnsAsync(returnValue);

        SetupScriptEvaluation(database);

        return database;
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

    private static Mock<IEventApiClient> CreateEventApiMock()
    {
        var eventApi = new Mock<IEventApiClient>();

        eventApi
            .Setup(api => api.GetEventByIdAsync(42))
            .ReturnsAsync(new EventApiResponseDto { EventId = 42 });

        return eventApi;
    }

    private static void VerifyStringSetCalled(
        Mock<IDatabase> database,
        string key,
        Times times)
    {
        database.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(redisKey => redisKey.ToString() == key),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>()), times);
    }

    private static HoldService CreateService(
        Mock<IDatabase> database,
        Mock<IEventApiClient> eventApi,
        bool canSell = true)
    {
        var redis = new Mock<IConnectionMultiplexer>();

        redis
            .Setup(connection => connection.GetDatabase(
                It.IsAny<int>(),
                It.IsAny<object?>()))
            .Returns(database.Object);

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