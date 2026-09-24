using EventAPI.Models;

namespace EventAPI.Repositories;

public interface IWishlistRepository
{
    Task<List<Wishlist>> GetByUserAsync(int userId);
    Task<Wishlist?> GetByUserAndEventAsync(int userId, int eventId);
    Task<List<(Event Event, int Count)>> GetAdminSummaryAsync(string? search, string? sort);
    Task AddAsync(Wishlist wishlist);
    void Remove(Wishlist wishlist);
    Task SaveChangesAsync();
}
