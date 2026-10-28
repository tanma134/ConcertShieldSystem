using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories;

public sealed class WishlistRepository(EventDbContext context) : IWishlistRepository
{
    public Task<List<Wishlist>> GetByUserAsync(int userId) => context.Wishlists
        .Include(x => x.Event)
        .Where(x => x.UserId == userId && !x.Event.IsDeleted)
        .OrderByDescending(x => x.CreatedAt)
        .ToListAsync();

    public Task<Wishlist?> GetByUserAndEventAsync(int userId, int eventId) => context.Wishlists
        .Include(x => x.Event)
        .SingleOrDefaultAsync(x => x.UserId == userId && x.EventId == eventId);

    public async Task<List<(Event Event, int Count)>> GetAdminSummaryAsync(string? search, string? sort)
    {
        var counts = await context.Wishlists.GroupBy(x => x.EventId).Select(group => new { EventId = group.Key, Count = group.Count() }).ToDictionaryAsync(x => x.EventId, x => x.Count);
        var events = context.Events.Where(x => !x.IsDeleted).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) events = events.Where(x => x.Title.ToLower().Contains(search.ToLower()));
        var items = await events.ToListAsync();
        var result = items.Select(item => (Event: item, Count: counts.TryGetValue(item.EventId, out var count) ? count : 0));
        return (sort?.ToLowerInvariant()) switch
        {
            "name-asc" => result.OrderBy(x => x.Event.Title).ToList(),
            "name-desc" => result.OrderByDescending(x => x.Event.Title).ToList(),
            "count-asc" => result.OrderBy(x => x.Count).ThenBy(x => x.Event.Title).ToList(),
            _ => result.OrderByDescending(x => x.Count).ThenBy(x => x.Event.Title).ToList(),
        };
    }

    public Task AddAsync(Wishlist wishlist) => context.Wishlists.AddAsync(wishlist).AsTask();
    public void Remove(Wishlist wishlist) => context.Wishlists.Remove(wishlist);
    public Task SaveChangesAsync() => context.SaveChangesAsync();
}
