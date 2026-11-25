using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewAPI.DTOs;
using ReviewAPI.Models;
using ReviewAPI.Repositories;
using ReviewAPI.Services;

namespace ReviewAPI.UnitTests;

internal static class ReviewServiceTestHelper
{
    internal static readonly IReadOnlyCollection<string> CustomerRoles = ["Customer"];

    internal static (
        ReviewService Service,
        Mock<IReviewRepository> Repository,
        Mock<IEventClient> Events,
        Mock<IUserClient> Users,
        Mock<ILogger<ReviewService>> Logger) CreateService()
    {
        var repository = new Mock<IReviewRepository>();
        var events = new Mock<IEventClient>();
        var users = new Mock<IUserClient>();
        var logger = new Mock<ILogger<ReviewService>>();

        repository.Setup(r => r.GetByEventAndUserAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((Review?)null);
        repository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        events.Setup(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)"Rock Night", (int?)3));
        users.Setup(u => u.GetUserNameAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Nguyen Van A");

        return (
            new ReviewService(repository.Object, events.Object, users.Object, logger.Object),
            repository,
            events,
            users,
            logger);
    }

    internal static CreateReviewRequest CreateRequest(int rating, string? comment) =>
        new() { EventId = 12, Rating = rating, Comment = comment };

    internal static Review CreateExistingReview(int userId = 7) =>
        new()
        {
            ReviewId = 20,
            EventId = 12,
            UserId = userId,
            Rating = 3,
            Comment = "old",
            CreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };

    internal static void VerifyCreateNotAttempted(
        Mock<IReviewRepository> repository,
        Mock<IEventClient> events)
    {
        repository.Verify(r => r.GetByEventAndUserAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
        events.Verify(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    internal static void VerifyDuplicateReviewWarning(Mock<ILogger<ReviewService>> logger)
    {
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Duplicate review")),
                It.IsAny<DbUpdateException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
