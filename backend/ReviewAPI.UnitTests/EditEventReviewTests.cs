using Moq;
using ReviewAPI.DTOs;
using Xunit;
using static ReviewAPI.UnitTests.ReviewServiceTestHelper;

namespace ReviewAPI.UnitTests;

public class EditEventReviewTests
{
    [Fact]
    public async Task UTCID01_UpdateAsync_UpdatesRatingCommentAndTimestamp()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);
        var before = DateTime.UtcNow;

        var response = await service.UpdateAsync(
            20,
            new UpdateReviewRequest { Rating = 4, Comment = "Updated text" },
            7,
            CustomerRoles);
        var after = DateTime.UtcNow;

        Assert.Equal(4, response.Rating);
        Assert.Equal("Updated text", response.Comment);
        Assert.Equal(4, review.Rating);
        Assert.Equal("Updated text", review.Comment);
        Assert.InRange(review.UpdatedAt!.Value, before, after);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_UpdateAsync_AcceptsMinimumRatingAndNullComment()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);

        var response = await service.UpdateAsync(
            20,
            new UpdateReviewRequest { Rating = 1, Comment = null },
            7,
            CustomerRoles);

        Assert.Equal(1, response.Rating);
        Assert.Null(response.Comment);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_UpdateAsync_AcceptsMaximumRatingAndCommentLength()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        var comment = new string('x', 1000);
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);

        var response = await service.UpdateAsync(
            20,
            new UpdateReviewRequest { Rating = 5, Comment = comment },
            7,
            CustomerRoles);

        Assert.Equal(5, response.Rating);
        Assert.Equal(comment, response.Comment);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID04_UpdateAsync_NormalizesWhitespaceCommentToNull()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);

        var response = await service.UpdateAsync(
            20,
            new UpdateReviewRequest { Rating = 4, Comment = "   " },
            7,
            CustomerRoles);

        Assert.Null(response.Comment);
        Assert.Null(review.Comment);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID05_UpdateAsync_RejectsNonCustomerRole()
    {
        var (service, repository, _, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdateAsync(
                20,
                new UpdateReviewRequest { Rating = 4, Comment = "Updated text" },
                7,
                ["Organizer"]));

        Assert.Equal("Only customers can manage reviews.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID06_UpdateAsync_RejectsRatingBelowMinimum()
    {
        var (service, repository, _, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(
                20,
                new UpdateReviewRequest { Rating = 0, Comment = "Updated text" },
                7,
                CustomerRoles));

        Assert.Equal("Rating must be between 1 and 5.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID07_UpdateAsync_RejectsRatingAboveMaximum()
    {
        var (service, repository, _, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(
                20,
                new UpdateReviewRequest { Rating = 6, Comment = "Updated text" },
                7,
                CustomerRoles));

        Assert.Equal("Rating must be between 1 and 5.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID08_UpdateAsync_RejectsCommentOverMaximumLength()
    {
        var (service, repository, _, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(
                20,
                new UpdateReviewRequest { Rating = 4, Comment = new string('x', 1001) },
                7,
                CustomerRoles));

        Assert.Equal("Comment cannot exceed 1000 characters.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID09_UpdateAsync_ThrowsWhenReviewDoesNotExist()
    {
        var (service, repository, _, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UpdateAsync(
                999,
                new UpdateReviewRequest { Rating = 4, Comment = "Updated text" },
                7,
                CustomerRoles));

        Assert.Equal("Review not found.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(999, false), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID10_UpdateAsync_RejectsReviewOwnedByAnotherUser()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview(8);
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdateAsync(
                20,
                new UpdateReviewRequest { Rating = 4, Comment = "Updated text" },
                7,
                CustomerRoles));

        Assert.Equal("You can only update your own review.", exception.Message);
        Assert.Equal(3, review.Rating);
        Assert.Equal("old", review.Comment);
        Assert.Null(review.UpdatedAt);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }
}
