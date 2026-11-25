using Microsoft.EntityFrameworkCore;
using Moq;
using ReviewAPI.Models;
using Xunit;
using static ReviewAPI.UnitTests.ReviewServiceTestHelper;

namespace ReviewAPI.UnitTests;

public class AddEventReviewTests
{
    [Fact]
    public async Task UTCID01_CreateAsync_CreatesReviewWithComment()
    {
        var (service, repository, _, _, _) = CreateService();
        Review? added = null;
        repository.Setup(r => r.AddAsync(It.IsAny<Review>()))
            .Callback<Review>(review => added = review)
            .Returns(Task.CompletedTask);

        var response = await service.CreateAsync(
            CreateRequest(4, "Great concert!"),
            7,
            CustomerRoles);

        Assert.Equal(12, response.EventId);
        Assert.Equal(7, response.UserId);
        Assert.Equal(4, response.Rating);
        Assert.Equal("Great concert!", response.Comment);
        Assert.Equal("Rock Night", response.EventName);
        Assert.Equal("Nguyen Van A", response.UserName);
        Assert.False(response.IsDeleted);
        Assert.Empty(response.Replies);
        Assert.NotNull(added);
        Assert.Equal(12, added.EventId);
        Assert.Equal(7, added.UserId);
        Assert.Equal(4, added.Rating);
        Assert.Equal("Great concert!", added.Comment);
        repository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_CreateAsync_AcceptsMinimumRatingAndNullComment()
    {
        var (service, repository, _, _, _) = CreateService();
        repository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);

        var response = await service.CreateAsync(
            CreateRequest(1, null),
            7,
            CustomerRoles);

        Assert.Equal(1, response.Rating);
        Assert.Null(response.Comment);
        repository.Verify(r => r.AddAsync(It.Is<Review>(review => review.Comment == null)), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_CreateAsync_AcceptsMaximumRatingAndCommentLength()
    {
        var (service, repository, _, _, _) = CreateService();
        var comment = new string('x', 1000);
        repository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);

        var response = await service.CreateAsync(
            CreateRequest(5, comment),
            7,
            CustomerRoles);

        Assert.Equal(5, response.Rating);
        Assert.Equal(comment, response.Comment);
        repository.Verify(r => r.AddAsync(It.Is<Review>(review => review.Comment!.Length == 1000)), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID04_CreateAsync_NormalizesWhitespaceCommentToNull()
    {
        var (service, repository, _, _, _) = CreateService();
        repository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);

        var response = await service.CreateAsync(
            CreateRequest(4, "   "),
            7,
            CustomerRoles);

        Assert.Null(response.Comment);
        repository.Verify(r => r.AddAsync(It.Is<Review>(review => review.Comment == null)), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID05_CreateAsync_TrimsComment()
    {
        var (service, repository, _, _, _) = CreateService();
        repository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);

        var response = await service.CreateAsync(
            CreateRequest(4, "  Nice  "),
            7,
            CustomerRoles);

        Assert.Equal("Nice", response.Comment);
        repository.Verify(r => r.AddAsync(It.Is<Review>(review => review.Comment == "Nice")), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID06_CreateAsync_AcceptsCustomerRoleCaseInsensitively()
    {
        var (service, repository, _, _, _) = CreateService();
        repository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);

        var response = await service.CreateAsync(
            CreateRequest(4, "Great concert!"),
            7,
            ["customer"]);

        Assert.Equal(4, response.Rating);
        repository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID07_CreateAsync_RejectsNonCustomerRole()
    {
        var (service, repository, events, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(CreateRequest(4, "Great concert!"), 7, ["Organizer"]));

        Assert.Equal("Only customers can manage reviews.", exception.Message);
        repository.Verify(r => r.GetByEventAndUserAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
        events.Verify(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID08_CreateAsync_RejectsEmptyRoles()
    {
        var (service, repository, events, _, _) = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(CreateRequest(4, "Great concert!"), 7, []));

        repository.Verify(r => r.GetByEventAndUserAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
        events.Verify(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID09_CreateAsync_RejectsRatingBelowMinimum()
    {
        var (service, repository, events, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(CreateRequest(0, "Great concert!"), 7, CustomerRoles));

        Assert.Equal("Rating must be between 1 and 5.", exception.Message);
        VerifyCreateNotAttempted(repository, events);
    }

    [Fact]
    public async Task UTCID10_CreateAsync_RejectsRatingAboveMaximum()
    {
        var (service, repository, events, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(CreateRequest(6, "Great concert!"), 7, CustomerRoles));

        Assert.Equal("Rating must be between 1 and 5.", exception.Message);
        VerifyCreateNotAttempted(repository, events);
    }

    [Fact]
    public async Task UTCID11_CreateAsync_RejectsCommentOverMaximumLength()
    {
        var (service, repository, events, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(CreateRequest(4, new string('x', 1001)), 7, CustomerRoles));

        Assert.Equal("Comment cannot exceed 1000 characters.", exception.Message);
        VerifyCreateNotAttempted(repository, events);
    }

    [Fact]
    public async Task UTCID12_CreateAsync_ThrowsWhenEventDoesNotExist()
    {
        var (service, repository, events, _, _) = CreateService();
        events.Setup(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (string?)null, (int?)null));

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateAsync(CreateRequest(4, "Great concert!"), 7, CustomerRoles));

        Assert.Equal("Event not found.", exception.Message);
        events.Verify(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.GetByEventAndUserAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID13_CreateAsync_RejectsAnExistingReviewByTheSameUser()
    {
        var (service, repository, _, _, _) = CreateService();
        repository.Setup(r => r.GetByEventAndUserAsync(12, 7))
            .ReturnsAsync(CreateExistingReview());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(CreateRequest(4, "Great concert!"), 7, CustomerRoles));

        Assert.Equal("You have already reviewed this event.", exception.Message);
        repository.Verify(r => r.GetByEventAndUserAsync(12, 7), Times.Once);
        repository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID14_CreateAsync_LogsAndTranslatesUniqueConstraintFailure()
    {
        var (service, repository, _, _, logger) = CreateService();
        repository.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateException("Duplicate review."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(CreateRequest(4, "Great concert!"), 7, CustomerRoles));

        Assert.Equal("You have already reviewed this event.", exception.Message);
        repository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
        VerifyDuplicateReviewWarning(logger);
    }
}
