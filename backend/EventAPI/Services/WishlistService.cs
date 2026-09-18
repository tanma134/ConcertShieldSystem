using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Services;

public sealed class WishlistService(IWishlistRepository repository) : IWishlistService
{
    public async Task<List<MyWishlistDTO>> GetMineAsync(int userId) => (await repository.GetByUserAsync(userId)).Select(Map).ToList();

    public async Task<WishlistStatusDTO> GetStatusAsync(int userId, int eventId)
        => new() { EventId = eventId, IsWishlisted = await repository.GetByUserAndEventAsync(userId, eventId) is not null };

    public async Task AddAsync(int userId, int eventId)
    {
        var existing = await repository.GetByUserAndEventAsync(userId, eventId);
        if (existing is not null) throw new InvalidOperationException("Event is already in your wishlist.");
        var wishlist = new Wishlist { UserId = userId, EventId = eventId };
        await repository.AddAsync(wishlist);
        try { await repository.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new InvalidOperationException("Event is already in your wishlist."); }
    }

    public async Task RemoveAsync(int userId, int eventId)
    {
        var existing = await repository.GetByUserAndEventAsync(userId, eventId);
        if (existing is null) throw new KeyNotFoundException("Event is not in your wishlist.");
        repository.Remove(existing);
        await repository.SaveChangesAsync();
    }

    public async Task<List<AdminWishlistSummaryDTO>> GetAdminSummaryAsync(string? search, string? sort)
        => (await repository.GetAdminSummaryAsync(search, sort)).Select(x => new AdminWishlistSummaryDTO { EventId = x.Event.EventId, EventName = x.Event.Title, EventImage = x.Event.BannerUrl ?? x.Event.PosterUrl, Status = x.Event.Status, WishlistCount = x.Count }).ToList();

    private static MyWishlistDTO Map(Wishlist wishlist) => new()
    {
        WishlistId = wishlist.WishlistId, EventId = wishlist.EventId, EventName = wishlist.Event.Title, Slug = wishlist.Event.Slug,
        EventImage = wishlist.Event.BannerUrl ?? wishlist.Event.PosterUrl, EventDate = wishlist.Event.StartsAt, EventEndDate = wishlist.Event.EndsAt,
        Location = string.Join(", ", new[] { wishlist.Event.LocationName, wishlist.Event.City }.Where(x => !string.IsNullOrWhiteSpace(x))), Status = wishlist.Event.Status, CreatedAt = wishlist.CreatedAt
    };
}
