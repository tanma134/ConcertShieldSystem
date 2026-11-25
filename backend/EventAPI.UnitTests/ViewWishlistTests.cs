using EventAPI.Models;
using EventAPI.Repositories;
using EventAPI.Services;
using Moq;
using Xunit;

namespace EventAPI.UnitTests;

public class ViewWishlistTests
{
    [Fact]
    public async Task UTCID01_GetMineAsync_MapsWishlistItemsInRepositoryOrder()
    {
        var startsAt = new DateTime(2026, 12, 10, 12, 0, 0, DateTimeKind.Utc);
        var endsAt = startsAt.AddHours(3);
        var createdAt = startsAt.AddDays(-10);
        var first = CreateWishlist(
            31,
            12,
            "Event A",
            "event-a",
            "banner-a.png",
            "poster-a.png",
            "My Dinh Stadium",
            "Ha Noi",
            startsAt,
            endsAt,
            createdAt);
        var second = CreateWishlist(
            32,
            13,
            "Event B",
            "event-b",
            "banner-b.png",
            null,
            "Venue B",
            "Da Nang",
            startsAt.AddDays(1),
            endsAt.AddDays(1),
            createdAt.AddDays(1));
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAsync(7)).ReturnsAsync([first, second]);
        var service = new WishlistService(repository.Object);

        var items = await service.GetMineAsync(7);

        Assert.Equal(2, items.Count);
        Assert.Equal(31, items[0].WishlistId);
        Assert.Equal(12, items[0].EventId);
        Assert.Equal("Event A", items[0].EventName);
        Assert.Equal("event-a", items[0].Slug);
        Assert.Equal("banner-a.png", items[0].EventImage);
        Assert.Equal(startsAt, items[0].EventDate);
        Assert.Equal(endsAt, items[0].EventEndDate);
        Assert.Equal("My Dinh Stadium, Ha Noi", items[0].Location);
        Assert.Equal("Published", items[0].Status);
        Assert.Equal(createdAt, items[0].CreatedAt);
        Assert.Equal(32, items[1].WishlistId);
        Assert.Equal("Event B", items[1].EventName);
        repository.Verify(r => r.GetByUserAsync(7), Times.Once);
    }

    [Fact]
    public async Task UTCID02_GetMineAsync_ReturnsEmptyListWhenRepositoryIsEmpty()
    {
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAsync(7)).ReturnsAsync([]);
        var service = new WishlistService(repository.Object);

        var items = await service.GetMineAsync(7);

        Assert.Empty(items);
        repository.Verify(r => r.GetByUserAsync(7), Times.Once);
    }

    [Fact]
    public async Task UTCID03_GetMineAsync_FallsBackToPosterWhenBannerIsNull()
    {
        var wishlist = CreateWishlist(
            31,
            12,
            "Event A",
            "event-a",
            null,
            "p.png",
            "Venue",
            "City",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow);
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAsync(7)).ReturnsAsync([wishlist]);
        var service = new WishlistService(repository.Object);

        var items = await service.GetMineAsync(7);

        Assert.Single(items);
        Assert.Equal("p.png", items[0].EventImage);
        repository.Verify(r => r.GetByUserAsync(7), Times.Once);
    }

    [Fact]
    public async Task UTCID04_GetMineAsync_ReturnsNullImageWhenBannerAndPosterAreNull()
    {
        var wishlist = CreateWishlist(
            31,
            12,
            "Event A",
            "event-a",
            null,
            null,
            "Venue",
            "City",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow);
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAsync(7)).ReturnsAsync([wishlist]);
        var service = new WishlistService(repository.Object);

        var items = await service.GetMineAsync(7);

        Assert.Single(items);
        Assert.Null(items[0].EventImage);
        repository.Verify(r => r.GetByUserAsync(7), Times.Once);
    }

    [Fact]
    public async Task UTCID05_GetMineAsync_OmitsBlankCityFromLocation()
    {
        var wishlist = CreateWishlist(
            31,
            12,
            "Event A",
            "event-a",
            null,
            null,
            "My Dinh Stadium",
            "   ",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow);
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAsync(7)).ReturnsAsync([wishlist]);
        var service = new WishlistService(repository.Object);

        var items = await service.GetMineAsync(7);

        Assert.Equal("My Dinh Stadium", items[0].Location);
        repository.Verify(r => r.GetByUserAsync(7), Times.Once);
    }

    [Fact]
    public async Task UTCID06_GetMineAsync_ReturnsEmptyLocationWhenVenueAndCityAreNull()
    {
        var wishlist = CreateWishlist(
            31,
            12,
            "Event A",
            "event-a",
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow);
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAsync(7)).ReturnsAsync([wishlist]);
        var service = new WishlistService(repository.Object);

        var items = await service.GetMineAsync(7);

        Assert.Equal(string.Empty, items[0].Location);
        repository.Verify(r => r.GetByUserAsync(7), Times.Once);
    }

    [Fact]
    public async Task UTCID07_GetMineAsync_PropagatesRepositoryException()
    {
        var repository = new Mock<IWishlistRepository>();
        repository.Setup(r => r.GetByUserAsync(7))
            .ThrowsAsync(new InvalidOperationException("Repository unavailable."));
        var service = new WishlistService(repository.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetMineAsync(7));

        Assert.Equal("Repository unavailable.", exception.Message);
        repository.Verify(r => r.GetByUserAsync(7), Times.Once);
    }

    private static Wishlist CreateWishlist(
        int wishlistId,
        int eventId,
        string title,
        string slug,
        string? bannerUrl,
        string? posterUrl,
        string? locationName,
        string? city,
        DateTime startsAt,
        DateTime endsAt,
        DateTime createdAt) =>
        new()
        {
            WishlistId = wishlistId,
            UserId = 7,
            EventId = eventId,
            CreatedAt = createdAt,
            Event = new Event
            {
                EventId = eventId,
                Title = title,
                Slug = slug,
                BannerUrl = bannerUrl,
                PosterUrl = posterUrl,
                LocationName = locationName,
                City = city,
                StartsAt = startsAt,
                EndsAt = endsAt,
                Status = "Published"
            }
        };
}
