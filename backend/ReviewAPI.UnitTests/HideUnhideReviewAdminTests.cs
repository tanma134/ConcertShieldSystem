using Microsoft.EntityFrameworkCore;
using Moq;
using ReviewAPI.Models;
using Xunit;
using static ReviewAPI.UnitTests.ReviewServiceTestHelper;

namespace ReviewAPI.UnitTests;

public sealed class HideUnhideReviewAdminTests
{
    [Fact]
    public async Task UTCID01_HidesVisibleReviewAndRecordsAdmin()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        var before = DateTime.UtcNow;

        await service.ModerateAsync(20, true, 2);

        var after = DateTime.UtcNow;
        Assert.True(review.IsDeleted);
        Assert.Equal(2, review.DeletedBy);
        Assert.InRange(review.DeletedAt!.Value, before, after);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_UnhidesHiddenReviewAndClearsDeletionMetadata()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        review.IsDeleted = true;
        review.DeletedAt = DateTime.UtcNow.AddDays(-1);
        review.DeletedBy = 1;
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);

        await service.ModerateAsync(20, false, 2);

        Assert.False(review.IsDeleted);
        Assert.Null(review.DeletedAt);
        Assert.Null(review.DeletedBy);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_HidingAlreadyHiddenReviewRefreshesDeletionMetadata()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        review.IsDeleted = true;
        review.DeletedAt = DateTime.UtcNow.AddDays(-1);
        review.DeletedBy = 1;
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        var before = DateTime.UtcNow;

        await service.ModerateAsync(20, true, 2);

        var after = DateTime.UtcNow;
        Assert.True(review.IsDeleted);
        Assert.Equal(2, review.DeletedBy);
        Assert.InRange(review.DeletedAt!.Value, before, after);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID04_UnhidingVisibleReviewKeepsDeletionMetadataClear()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);

        await service.ModerateAsync(20, false, 2);

        Assert.False(review.IsDeleted);
        Assert.Null(review.DeletedAt);
        Assert.Null(review.DeletedBy);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID05_ThrowsWhenReviewDoesNotExist()
    {
        var (service, repository, _, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ModerateAsync(999, true, 2));

        Assert.Equal("Review not found.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(999, true), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID06_PropagatesSaveChangesException()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        repository.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateException("Database unavailable."));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => service.ModerateAsync(20, true, 2));

        Assert.Equal("Database unavailable.", exception.Message);
        Assert.True(review.IsDeleted);
        Assert.Equal(2, review.DeletedBy);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
