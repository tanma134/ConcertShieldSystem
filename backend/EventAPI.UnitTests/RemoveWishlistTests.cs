using EventAPI.Models;
using EventAPI.Repositories;
using EventAPI.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EventAPI.UnitTests;

public class RemoveWishlistTests
{
    [Fact]
    public async Task UTCID01_RemoveAsync_RemovesExistingWishlistAndSavesChanges()
    {
        var repository = new Mock<IWishlistRepository>();
        var wishlist = new Wishlist { WishlistId = 30, UserId = 7, EventId = 12 };
        repository.Setup(r => r.GetByUserAndEventAsync(7, 12)).ReturnsAsync(wishlist);
        repository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        var service = new WishlistService(repository.Object);

        await service.RemoveAsync(7, 12);

        repository.Verify(r => r.Remove(wishlist), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_RemoveAsync_ThrowsWhenWishlistEntryDoesNotExist()
    {
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAndEventAsync(7, 12)).ReturnsAsync((Wishlist?)null);
        var service = new WishlistService(repository.Object);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.RemoveAsync(7, 12));

        Assert.Equal("Event is not in your wishlist.", exception.Message);
        repository.Verify(r => r.Remove(It.IsAny<Wishlist>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID03_RemoveAsync_PropagatesSaveChangesException()
    {
        var repository = new Mock<IWishlistRepository>();
        var wishlist = new Wishlist { WishlistId = 30, UserId = 7, EventId = 12 };
        repository.Setup(r => r.GetByUserAndEventAsync(7, 12)).ReturnsAsync(wishlist);
        repository.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateException("Database unavailable."));
        var service = new WishlistService(repository.Object);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => service.RemoveAsync(7, 12));

        Assert.Equal("Database unavailable.", exception.Message);
        repository.Verify(r => r.Remove(wishlist), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
