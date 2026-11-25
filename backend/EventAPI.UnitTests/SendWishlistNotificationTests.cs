using System.Net;
using System.Reflection;
using System.Text.Json;
using EventAPI.Data;
using EventAPI.Models;
using EventAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace EventAPI.UnitTests;

public sealed class SendWishlistNotificationTests
{
    [PostgresFact]
    public async Task UTCID01_SendsReminderAndMarksDeliverySentOnHttp200()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture);
        using var harness = CreateHarness();

        var processed = await DeliverNextAsync(harness.Worker, fixture.Context);

        Assert.True(processed);
        AssertRequest(harness.Handler, seeded.EventId, 7, "Rock Night", seeded.Slug, seeded.StartsAt);
        var delivery = await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7);
        Assert.NotNull(delivery.SentAt);
        Assert.Equal(0, delivery.AttemptCount);
    }

    [PostgresFact]
    public async Task UTCID02_TreatsHttp204AsSuccessfulDelivery()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture);
        using var harness = CreateHarness(HttpStatusCode.NoContent);

        var processed = await DeliverNextAsync(harness.Worker, fixture.Context);

        Assert.True(processed);
        Assert.NotNull((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
        Assert.Equal(1, harness.Handler.RequestCount);
    }

    [PostgresFact]
    public async Task UTCID03_ProcessesOnlyTheNextOrderedEligibleDelivery()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEventAsync(fixture, TimeSpan.FromHours(12));
        await AddDeliveryAsync(fixture.Context, seeded.EventId, 8, seeded.StartsAt, DateTime.UtcNow.AddMinutes(-1));
        await AddDeliveryAsync(fixture.Context, seeded.EventId, 7, seeded.StartsAt, DateTime.UtcNow.AddMinutes(-1));
        using var harness = CreateHarness();

        var processed = await DeliverNextAsync(harness.Worker, fixture.Context);

        Assert.True(processed);
        AssertRequest(harness.Handler, seeded.EventId, 7, "Rock Night", seeded.Slug, seeded.StartsAt);
        Assert.NotNull((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
        var untouched = await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 8);
        Assert.Null(untouched.SentAt);
        Assert.Equal(0, untouched.AttemptCount);
    }

    [PostgresFact]
    public async Task UTCID04_ReturnsFalseWhenThereAreNoDeliveryRows()
    {
        using var fixture = new EventFlowFixture();
        using var harness = CreateHarness();

        var processed = await DeliverNextAsync(harness.Worker, fixture.Context);

        Assert.False(processed);
        Assert.Equal(0, harness.Handler.RequestCount);
    }

    [PostgresFact]
    public async Task UTCID05_SkipsAlreadySentDelivery()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, sentAt: DateTime.UtcNow.AddMinutes(-1));
        using var harness = CreateHarness();

        var processed = await DeliverNextAsync(harness.Worker, fixture.Context);

        Assert.False(processed);
        Assert.Equal(0, harness.Handler.RequestCount);
        Assert.NotNull((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
    }

    [PostgresFact]
    public async Task UTCID06_SkipsDeliveryBeforeItsNextAttemptTime()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, nextAttemptAt: DateTime.UtcNow.AddMinutes(10));
        using var harness = CreateHarness();

        var processed = await DeliverNextAsync(harness.Worker, fixture.Context);

        Assert.False(processed);
        Assert.Equal(0, harness.Handler.RequestCount);
        Assert.Equal(0, (await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).AttemptCount);
    }

    [PostgresFact]
    public async Task UTCID07_SkipsDraftEvent()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, status: "Draft");
        using var harness = CreateHarness();

        Assert.False(await DeliverNextAsync(harness.Worker, fixture.Context));

        Assert.Equal(0, harness.Handler.RequestCount);
        Assert.Null((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
    }

    [PostgresFact]
    public async Task UTCID08_SkipsDeletedEvent()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, isDeleted: true);
        using var harness = CreateHarness();

        Assert.False(await DeliverNextAsync(harness.Worker, fixture.Context));

        Assert.Equal(0, harness.Handler.RequestCount);
        Assert.Null((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
    }

    [PostgresFact]
    public async Task UTCID09_SkipsEventsMoreThanTwentyFourHoursAway()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, startOffset: TimeSpan.FromHours(24).Add(TimeSpan.FromMinutes(1)));
        using var harness = CreateHarness();

        Assert.False(await DeliverNextAsync(harness.Worker, fixture.Context));

        Assert.Equal(0, harness.Handler.RequestCount);
        Assert.Null((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
    }

    [PostgresFact]
    public async Task UTCID10_DeliversEventJustInsideTwentyFourHourWindow()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, startOffset: TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)));
        using var harness = CreateHarness();

        Assert.True(await DeliverNextAsync(harness.Worker, fixture.Context));

        Assert.Equal(1, harness.Handler.RequestCount);
        Assert.NotNull((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
    }

    [PostgresFact]
    public async Task UTCID11_SkipsEventsThatHaveAlreadyStarted()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, startOffset: TimeSpan.FromHours(-1));
        using var harness = CreateHarness();

        Assert.False(await DeliverNextAsync(harness.Worker, fixture.Context));

        Assert.Equal(0, harness.Handler.RequestCount);
        Assert.Null((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
    }

    [PostgresFact]
    public async Task UTCID12_SkipsDeliveryWhenWishlistEntryNoLongerExists()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, addWishlist: false);
        using var harness = CreateHarness();

        Assert.False(await DeliverNextAsync(harness.Worker, fixture.Context));

        Assert.Equal(0, harness.Handler.RequestCount);
        Assert.Null((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7)).SentAt);
    }

    [PostgresFact]
    public async Task UTCID13_SkipsDeliveryForRescheduledEvent()
    {
        using var fixture = new EventFlowFixture();
        var originalStart = DateTime.UtcNow.AddHours(12);
        var seeded = await SeedEventAsync(fixture, TimeSpan.FromHours(12));
        await AddDeliveryAsync(fixture.Context, seeded.EventId, 7, originalStart.AddHours(-1), DateTime.UtcNow.AddMinutes(-1));
        using var harness = CreateHarness();

        Assert.False(await DeliverNextAsync(harness.Worker, fixture.Context));

        Assert.Equal(0, harness.Handler.RequestCount);
        Assert.Null((await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7, originalStart.AddHours(-1))).SentAt);
    }

    [PostgresFact]
    public async Task UTCID14_RetriesAfterUnauthorizedEmailEndpointResponse()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture);
        using var harness = CreateHarness(HttpStatusCode.Unauthorized);
        var before = DateTime.UtcNow;

        Assert.True(await DeliverNextAsync(harness.Worker, fixture.Context));

        var after = DateTime.UtcNow;
        var delivery = await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7);
        AssertRetry(delivery, before, after, 1, 10);
        VerifyFailureLogged(harness.Logger);
    }

    [PostgresFact]
    public async Task UTCID15_RetriesAfterInternalServerError()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture);
        using var harness = CreateHarness(HttpStatusCode.InternalServerError);
        var before = DateTime.UtcNow;

        Assert.True(await DeliverNextAsync(harness.Worker, fixture.Context));

        var after = DateTime.UtcNow;
        AssertRetry(await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7), before, after, 1, 10);
        VerifyFailureLogged(harness.Logger);
    }

    [PostgresFact]
    public async Task UTCID16_RetriesAfterHttpRequestException()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture);
        using var harness = CreateHarness((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Service unavailable.")));
        var before = DateTime.UtcNow;

        Assert.True(await DeliverNextAsync(harness.Worker, fixture.Context));

        var after = DateTime.UtcNow;
        AssertRetry(await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7), before, after, 1, 10);
        VerifyFailureLogged(harness.Logger);
    }

    [PostgresFact]
    public async Task UTCID17_RetriesAfterUncancelledHttpTimeout()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture);
        using var harness = CreateHarness((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("HTTP request timed out.")));
        var before = DateTime.UtcNow;

        Assert.True(await DeliverNextAsync(harness.Worker, fixture.Context));

        var after = DateTime.UtcNow;
        AssertRetry(await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7), before, after, 1, 10);
        VerifyFailureLogged(harness.Logger);
    }

    [PostgresFact]
    public async Task UTCID18_UsesExponentialBackoffForAttemptCountFour()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, attemptCount: 4);
        using var harness = CreateHarness(HttpStatusCode.InternalServerError);
        var before = DateTime.UtcNow;

        Assert.True(await DeliverNextAsync(harness.Worker, fixture.Context));

        var after = DateTime.UtcNow;
        AssertRetry(await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7), before, after, 5, 160);
        VerifyFailureLogged(harness.Logger);
    }

    [PostgresFact]
    public async Task UTCID19_CapsExponentialBackoffAtFiveMinutes()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture, attemptCount: 5);
        using var harness = CreateHarness(HttpStatusCode.InternalServerError);
        var before = DateTime.UtcNow;

        Assert.True(await DeliverNextAsync(harness.Worker, fixture.Context));

        var after = DateTime.UtcNow;
        AssertRetry(await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7), before, after, 6, 300);
        VerifyFailureLogged(harness.Logger);
    }

    [PostgresFact]
    public async Task UTCID20_PropagatesCancellationAndRollsBackDeliveryChanges()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture);
        using var cancellation = new CancellationTokenSource();
        using var harness = CreateHarness((_, token) =>
        {
            cancellation.Cancel();
            return Task.FromException<HttpResponseMessage>(new OperationCanceledException(token));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DeliverNextAsync(harness.Worker, fixture.Context, cancellation.Token));

        var delivery = await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7);
        Assert.Null(delivery.SentAt);
        Assert.Equal(0, delivery.AttemptCount);
        Assert.Equal(0, harness.Logger.Invocations.Count(invocation =>
            invocation.Method.Name == nameof(ILogger.Log) &&
            invocation.Arguments[0] is LogLevel level &&
            level == LogLevel.Warning));
    }

    [PostgresFact]
    public async Task UTCID21_ReturnsFalseWhenEligibleDeliveryIsLockedElsewhere()
    {
        using var fixture = new EventFlowFixture();
        var seeded = await SeedEligibleDeliveryAsync(fixture);
        await using var lockConnection = new NpgsqlConnection(fixture.ConnectionString);
        await lockConnection.OpenAsync();
        await using var lockTransaction = await lockConnection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
            "SELECT event_id FROM public.event_reminder_deliveries WHERE event_id = @eventId AND user_id = 7 FOR UPDATE",
            lockConnection,
            lockTransaction))
        {
            command.Parameters.AddWithValue("eventId", seeded.EventId);
            Assert.NotNull(await command.ExecuteScalarAsync());
        }

        using var harness = CreateHarness();
        var processed = await DeliverNextAsync(harness.Worker, fixture.Context);

        Assert.False(processed);
        Assert.Equal(0, harness.Handler.RequestCount);
        var delivery = await ReloadDeliveryAsync(fixture.Context, seeded.EventId, 7);
        Assert.Null(delivery.SentAt);
        Assert.Equal(0, delivery.AttemptCount);
    }

    private static async Task<(int EventId, string Slug, DateTime StartsAt)> SeedEligibleDeliveryAsync(
        EventFlowFixture fixture,
        TimeSpan? startOffset = null,
        DateTime? nextAttemptAt = null,
        DateTime? sentAt = null,
        int attemptCount = 0,
        string status = "Published",
        bool isDeleted = false,
        bool addWishlist = true) =>
        await SeedDeliveryAsync(
            fixture,
            startOffset ?? TimeSpan.FromHours(12),
            nextAttemptAt,
            sentAt,
            attemptCount,
            status,
            isDeleted,
            addWishlist);

    private static async Task<(int EventId, string Slug, DateTime StartsAt)> SeedDeliveryAsync(
        EventFlowFixture fixture,
        TimeSpan startOffset,
        DateTime? nextAttemptAt = null,
        DateTime? sentAt = null,
        int attemptCount = 0,
        string status = "Published",
        bool isDeleted = false,
        bool addWishlist = true)
    {
        var seeded = await SeedEventAsync(fixture, startOffset, status, isDeleted);
        if (addWishlist)
            fixture.Context.Wishlists.Add(new Wishlist { EventId = seeded.EventId, UserId = 7 });
        await AddDeliveryAsync(
            fixture.Context,
            seeded.EventId,
            7,
            seeded.StartsAt,
            nextAttemptAt ?? DateTime.UtcNow.AddMinutes(-1),
            sentAt,
            attemptCount);
        return seeded;
    }

    private static async Task<(int EventId, string Slug, DateTime StartsAt)> SeedEventAsync(
        EventFlowFixture fixture,
        TimeSpan startOffset,
        string status = "Published",
        bool isDeleted = false)
    {
        var startsAt = DateTime.UtcNow.Add(startOffset);
        var slug = "reminder-" + Guid.NewGuid().ToString("N");
        var eventEntity = new Event
        {
            OrganizerId = 3,
            Title = "Rock Night",
            Slug = slug,
            LocationName = "Can Tho Convention Center",
            Address = "1 Hoa Binh Ave",
            City = "Can Tho",
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(3),
            Timezone = "Asia/Ho_Chi_Minh",
            Status = status,
            IsDeleted = isDeleted
        };
        fixture.Context.Events.Add(eventEntity);
        await fixture.Context.SaveChangesAsync();
        return (eventEntity.EventId, slug, startsAt);
    }

    private static Task AddDeliveryAsync(
        EventDbContext context,
        int eventId,
        int userId,
        DateTime scheduledStartAt,
        DateTime nextAttemptAt,
        DateTime? sentAt = null,
        int attemptCount = 0)
    {
        context.EventReminderDeliveries.Add(new EventReminderDelivery
        {
            EventId = eventId,
            UserId = userId,
            ScheduledStartAt = scheduledStartAt,
            NextAttemptAt = nextAttemptAt,
            SentAt = sentAt,
            AttemptCount = attemptCount
        });
        return context.SaveChangesAsync();
    }

    private static async Task<EventReminderDelivery> ReloadDeliveryAsync(
        EventDbContext context,
        int eventId,
        int userId,
        DateTime? scheduledStartAt = null)
    {
        context.ChangeTracker.Clear();
        return await context.EventReminderDeliveries.SingleAsync(delivery =>
            delivery.EventId == eventId &&
            delivery.UserId == userId &&
            (scheduledStartAt == null || delivery.ScheduledStartAt == scheduledStartAt));
    }

    private static WorkerHarness CreateHarness(HttpStatusCode responseCode = HttpStatusCode.OK) =>
        CreateHarness((_, _) => Task.FromResult(new HttpResponseMessage(responseCode)));

    private static WorkerHarness CreateHarness(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        var handler = new RecordingHttpHandler(responseFactory);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5010/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("EventReminderEmail")).Returns(client);
        var logger = new Mock<ILogger<UpcomingEventReminderWorker>>();
        var worker = new UpcomingEventReminderWorker(
            Mock.Of<IServiceScopeFactory>(),
            factory.Object,
            logger.Object);
        return new WorkerHarness(worker, handler, logger, client);
    }

    private static async Task<bool> DeliverNextAsync(
        UpcomingEventReminderWorker worker,
        EventDbContext context,
        CancellationToken cancellationToken = default)
    {
        var method = typeof(UpcomingEventReminderWorker).GetMethod(
            "DeliverNextReminderAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task<bool>>(
            method.Invoke(worker, [context, cancellationToken]));
        return await task;
    }

    private static void AssertRequest(
        RecordingHttpHandler handler,
        int eventId,
        int userId,
        string title,
        string slug,
        DateTime startsAt)
    {
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("/api/internal/event-reminders", handler.RequestUri!.AbsolutePath);
        using var document = JsonDocument.Parse(handler.RequestBody!);
        var body = document.RootElement;
        Assert.Equal(eventId, body.GetProperty("eventId").GetInt32());
        Assert.Equal(userId, body.GetProperty("userId").GetInt32());
        Assert.Equal(title, body.GetProperty("eventTitle").GetString());
        Assert.Equal(slug, body.GetProperty("eventSlug").GetString());
        Assert.Equal("Asia/Ho_Chi_Minh", body.GetProperty("timezone").GetString());
        Assert.Equal(startsAt, body.GetProperty("startsAt").GetDateTime());
    }

    private static void AssertRetry(
        EventReminderDelivery delivery,
        DateTime before,
        DateTime after,
        int expectedAttemptCount,
        int expectedRetrySeconds)
    {
        Assert.Null(delivery.SentAt);
        Assert.Equal(expectedAttemptCount, delivery.AttemptCount);
        Assert.InRange(
            delivery.NextAttemptAt,
            before.AddSeconds(expectedRetrySeconds - 1),
            after.AddSeconds(expectedRetrySeconds + 1));
    }

    private static void VerifyFailureLogged(Mock<ILogger<UpcomingEventReminderWorker>> logger) =>
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed sending event reminder")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

    private sealed class WorkerHarness(
        UpcomingEventReminderWorker worker,
        RecordingHttpHandler handler,
        Mock<ILogger<UpcomingEventReminderWorker>> logger,
        HttpClient client) : IDisposable
    {
        public UpcomingEventReminderWorker Worker { get; } = worker;
        public RecordingHttpHandler Handler { get; } = handler;
        public Mock<ILogger<UpcomingEventReminderWorker>> Logger { get; } = logger;

        public void Dispose() => client.Dispose();
    }

    private sealed class RecordingHttpHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return await responseFactory(request, cancellationToken);
        }
    }
}
