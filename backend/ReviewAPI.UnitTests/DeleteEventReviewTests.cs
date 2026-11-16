using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static ReviewAPI.UnitTests.ReviewServiceTestHelper;

namespace ReviewAPI.UnitTests;

public class DeleteEventReviewTests
{
    [Fact]
    public async Task UTCID01_DeleteAsync_SoftDeletesReviewOwnedByCustomer()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);
        var before = DateTime.UtcNow;

        await service.DeleteAsync(20, 7, CustomerRoles);
        var after = DateTime.UtcNow;

        Assert.True(review.IsDeleted);
        Assert.Equal(7, review.DeletedBy);
        Assert.InRange(review.DeletedAt!.Value, before, after);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_DeleteAsync_AcceptsCustomerRoleCaseInsensitively()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);

        await service.DeleteAsync(20, 7, ["customer"]);

        Assert.True(review.IsDeleted);
        Assert.Equal(7, review.DeletedBy);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_DeleteAsync_RejectsNonCustomerRole()
    {
        var (service, repository, _, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DeleteAsync(20, 7, ["Organizer"]));

        Assert.Equal("Only customers can manage reviews.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID04_DeleteAsync_ThrowsWhenReviewDoesNotExist()
    {
        var (service, repository, _, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.DeleteAsync(999, 7, CustomerRoles));

        Assert.Equal("Review not found.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(999, false), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID05_DeleteAsync_RejectsReviewOwnedByAnotherUser()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview(8);
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DeleteAsync(20, 7, CustomerRoles));

        Assert.Equal("You can only delete your own review.", exception.Message);
        Assert.False(review.IsDeleted);
        Assert.Null(review.DeletedAt);
        Assert.Null(review.DeletedBy);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID06_DeleteAsync_PropagatesSaveChangesException()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, false)).ReturnsAsync(review);
        repository.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateException("Database unavailable."));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => service.DeleteAsync(20, 7, CustomerRoles));

        Assert.Equal("Database unavailable.", exception.Message);
        Assert.True(review.IsDeleted);
        Assert.Equal(7, review.DeletedBy);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
