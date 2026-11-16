using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewAPI.Data;
using ReviewAPI.Models;
using ReviewAPI.Repositories;
using ReviewAPI.Services;
using Xunit;

namespace ReviewAPI.UnitTests;

public sealed class ViewEventReviewListTests
{
    [Fact]
    public async Task UTCID01_ReturnsMappedReviewsInRepositoryOrder()
    {
        var (service, repository, events, users) = CreateService();
        var reviews = new[]
        {
            CreateReview(3, 12, 9, 5, "Amazing", new DateTime(2026, 2, 3, 12, 0, 0, DateTimeKind.Utc)),
            CreateReview(2, 12, 8, 3, null, new DateTime(2026, 2, 3, 11, 0, 0, DateTimeKind.Utc)),
            CreateReview(1, 12, 7, 4, "Good", new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 4, 10, 0, 0, DateTimeKind.Utc))
        };
        repository.Setup(r => r.GetActiveByEventAsync(12)).ReturnsAsync(reviews.ToList());
        events.Setup(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)"Rock Night", (int?)3));
        SetupUserNames(users);

        var result = await service.GetByEventAsync(12);

        Assert.Equal(new[] { 3, 2, 1 }, result.Select(x => x.ReviewId));
        Assert.Equal(new[] { 9, 8, 7 }, result.Select(x => x.UserId));
        Assert.Equal(new[] { "Le Van C", "Tran Thi B", "Nguyen Van A" }, result.Select(x => x.UserName));
        Assert.Equal(new[] { 5, 3, 4 }, result.Select(x => x.Rating));
        Assert.Equal(new[] { "Amazing", null, "Good" }, result.Select(x => x.Comment));
        Assert.All(result, item => Assert.Equal("Rock Night", item.EventName));
        Assert.Equal(new DateTime(2026, 2, 4, 10, 0, 0, DateTimeKind.Utc), result[2].UpdatedAt);
        Assert.All(result, item => Assert.False(item.IsDeleted));
        Assert.All(result, item => Assert.Empty(item.Replies));
        repository.Verify(r => r.GetActiveByEventAsync(12), Times.Once);
    }

    [Fact]
    public async Task UTCID02_ReturnsEmptyListWithoutCallingClients()
    {
        var (service, repository, events, users) = CreateService();
        repository.Setup(r => r.GetActiveByEventAsync(13)).ReturnsAsync([]);

        var result = await service.GetByEventAsync(13);

        Assert.Empty(result);
        repository.Verify(r => r.GetActiveByEventAsync(13), Times.Once);
        events.Verify(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        users.Verify(u => u.GetUserNameAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID03_MapsOnlyNonDeletedRepliesInAscendingOrder()
    {
        var (service, repository, events, users) = CreateService();
        var review = CreateReview(3, 12, 9, 5, "Amazing", new DateTime(2026, 2, 3, 12, 0, 0, DateTimeKind.Utc));
        review.Replies =
        [
            CreateReply(10, 9, "Organizer", "Organizer reply", new DateTime(2026, 2, 3, 10, 5, 0, DateTimeKind.Utc)),
            CreateReply(11, 1, "Admin", "Admin reply", new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc)),
            new ReviewReply
            {
                ReplyId = 12,
                ReviewId = 3,
                UserId = 1,
                Role = "Admin",
                Comment = "Deleted reply",
                IsDeleted = true,
                CreatedAt = new DateTime(2026, 2, 3, 10, 7, 0, DateTimeKind.Utc)
            }
        ];
        repository.Setup(r => r.GetActiveByEventAsync(12)).ReturnsAsync([review]);
        events.Setup(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)"Rock Night", (int?)3));
        SetupUserNames(users);
        users.Setup(u => u.GetUserNameAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Admin");

        var result = await service.GetByEventAsync(12);

        var replies = Assert.Single(result).Replies;
        Assert.Equal(new[] { 11, 10 }, replies.Select(x => x.ReplyId));
        Assert.Equal(new[] { "Admin", "Organizer" }, replies.Select(x => x.Role));
        Assert.Equal(new[] { "Admin reply", "Organizer reply" }, replies.Select(x => x.Comment));
        Assert.Equal(new[] { "Admin", "Le Van C" }, replies.Select(x => x.UserName));
        Assert.Equal(new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc), replies[0].CreatedAt);
    }

    [Fact]
    public async Task UTCID04_ReturnsSingleReview()
    {
        var (service, repository, events, users) = CreateService();
        repository.Setup(r => r.GetActiveByEventAsync(12))
            .ReturnsAsync([CreateReview(1, 12, 7, 4, "Good", DateTime.UtcNow)]);
        events.Setup(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)"Rock Night", (int?)3));
        SetupUserNames(users);

        var result = await service.GetByEventAsync(12);

        Assert.Equal(1, Assert.Single(result).ReviewId);
    }

    [Fact]
    public async Task UTCID05_LeavesEventNameNullWhenEventIsUnavailable()
    {
        var (service, repository, events, users) = CreateService();
        repository.Setup(r => r.GetActiveByEventAsync(12))
            .ReturnsAsync([CreateReview(1, 12, 7, 4, "Good", DateTime.UtcNow)]);
        events.Setup(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (string?)null, (int?)null));
        SetupUserNames(users);

        var result = await service.GetByEventAsync(12);

        Assert.Null(Assert.Single(result).EventName);
    }

    [Fact]
    public async Task UTCID06_UsesFallbackUserNamesWhenIdentityLookupFails()
    {
        var (service, repository, events, users) = CreateService();
        repository.Setup(r => r.GetActiveByEventAsync(12)).ReturnsAsync(
        [
            CreateReview(3, 12, 9, 5, "Amazing", DateTime.UtcNow),
            CreateReview(2, 12, 8, 3, null, DateTime.UtcNow.AddMinutes(-1)),
            CreateReview(1, 12, 7, 4, "Good", DateTime.UtcNow.AddMinutes(-2))
        ]);
        events.Setup(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)"Rock Night", (int?)3));
        users.Setup(u => u.GetUserNameAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int userId, CancellationToken _) => Task.FromResult($"User #{userId}"));

        var result = await service.GetByEventAsync(12);

        Assert.Equal(new[] { "User #9", "User #8", "User #7" }, result.Select(x => x.UserName));
    }

    [Fact]
    public async Task UTCID07_PropagatesRepositoryException()
    {
        var (service, repository, _, _) = CreateService();
        repository.Setup(r => r.GetActiveByEventAsync(12))
            .ThrowsAsync(new InvalidOperationException("Database unavailable."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetByEventAsync(12));

        Assert.Equal("Database unavailable.", exception.Message);
        repository.Verify(r => r.GetActiveByEventAsync(12), Times.Once);
    }

    [Fact]
    public async Task UTCID08_RepositoryExcludesHiddenReviewsOrdersNewestFirstAndIncludesNoReplies()
    {
        var options = new DbContextOptionsBuilder<ReviewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ReviewDbContext(options);
        context.Reviews.AddRange(
            CreateReview(1, 12, 7, 4, "Visible old", new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc)),
            CreateReview(2, 12, 8, 3, "Hidden", new DateTime(2026, 2, 3, 11, 0, 0, DateTimeKind.Utc), isDeleted: true),
            CreateReview(3, 12, 9, 5, "Visible new", new DateTime(2026, 2, 3, 12, 0, 0, DateTimeKind.Utc)),
            CreateReview(4, 13, 10, 2, "Other event", new DateTime(2026, 2, 3, 13, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();

        var events = new Mock<IEventClient>();
        var users = new Mock<IUserClient>();
        events.Setup(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)"Rock Night", (int?)3));
        users.Setup(u => u.GetUserNameAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Reviewer");
        var service = new ReviewService(
            new ReviewRepository(context),
            events.Object,
            users.Object,
            Mock.Of<ILogger<ReviewService>>());

        var result = await service.GetByEventAsync(12);

        Assert.Equal(new[] { 3, 1 }, result.Select(x => x.ReviewId));
        Assert.Equal(new[] { "Visible new", "Visible old" }, result.Select(x => x.Comment));
        Assert.All(result, item => Assert.False(item.IsDeleted));
        Assert.All(result, item => Assert.Empty(item.Replies));
    }

    private static (ReviewService Service, Mock<IReviewRepository> Repository, Mock<IEventClient> Events, Mock<IUserClient> Users) CreateService()
    {
        var repository = new Mock<IReviewRepository>();
        var events = new Mock<IEventClient>();
        var users = new Mock<IUserClient>();
        var service = new ReviewService(
            repository.Object,
            events.Object,
            users.Object,
            Mock.Of<ILogger<ReviewService>>());
        return (service, repository, events, users);
    }

    private static void SetupUserNames(Mock<IUserClient> users)
    {
        users.Setup(u => u.GetUserNameAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync("Nguyen Van A");
        users.Setup(u => u.GetUserNameAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync("Tran Thi B");
        users.Setup(u => u.GetUserNameAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync("Le Van C");
    }

    private static Review CreateReview(
        int reviewId,
        int eventId,
        int userId,
        int rating,
        string? comment,
        DateTime createdAt,
        DateTime? updatedAt = null,
        bool isDeleted = false) =>
        new()
        {
            ReviewId = reviewId,
            EventId = eventId,
            UserId = userId,
            Rating = rating,
            Comment = comment,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            IsDeleted = isDeleted
        };

    private static ReviewReply CreateReply(
        int replyId,
        int userId,
        string role,
        string comment,
        DateTime createdAt) =>
        new()
        {
            ReplyId = replyId,
            ReviewId = 3,
            UserId = userId,
            Role = role,
            Comment = comment,
            CreatedAt = createdAt
        };
}
