using EventAPI.Models;
using EventAPI.Repositories;
using EventAPI.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EventAPI.UnitTests;

public class AddWishlistTests
{
    [Fact]
    public async Task UTCID01_AddAsync_AddsWishlistAndSavesChanges()
    {
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAndEventAsync(7, 12)).ReturnsAsync((Wishlist?)null);
        repository.Setup(r => r.AddAsync(It.IsAny<Wishlist>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        var service = new WishlistService(repository.Object);

        await service.AddAsync(7, 12);

        repository.Verify(
            r => r.AddAsync(It.Is<Wishlist>(wishlist => wishlist.UserId == 7 && wishlist.EventId == 12)),
            Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_AddAsync_RejectsExistingWishlistEntry()
    {
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAndEventAsync(7, 12))
            .ReturnsAsync(new Wishlist { UserId = 7, EventId = 12 });
        var service = new WishlistService(repository.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddAsync(7, 12));

        Assert.Equal("Event is already in your wishlist.", exception.Message);
        repository.Verify(r => r.AddAsync(It.IsAny<Wishlist>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID03_AddAsync_TranslatesUniqueConstraintFailure()
    {
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAndEventAsync(7, 12)).ReturnsAsync((Wishlist?)null);
        repository.Setup(r => r.AddAsync(It.IsAny<Wishlist>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateException("Unique wishlist constraint violated."));
        var service = new WishlistService(repository.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddAsync(7, 12));

        Assert.Equal("Event is already in your wishlist.", exception.Message);
        repository.Verify(
            r => r.AddAsync(It.Is<Wishlist>(wishlist => wishlist.UserId == 7 && wishlist.EventId == 12)),
            Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID04_AddAsync_AllowsDifferentUserToWishlistSameEvent()
    {
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAndEventAsync(8, 12)).ReturnsAsync((Wishlist?)null);
        repository.Setup(r => r.AddAsync(It.IsAny<Wishlist>())).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        var service = new WishlistService(repository.Object);

        await service.AddAsync(8, 12);

        repository.Verify(
            r => r.AddAsync(It.Is<Wishlist>(wishlist => wishlist.UserId == 8 && wishlist.EventId == 12)),
            Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
