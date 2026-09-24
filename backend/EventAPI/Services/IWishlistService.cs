using EventAPI.DTOs;

namespace EventAPI.Services;

public interface IWishlistService
{
    Task<List<MyWishlistDTO>> GetMineAsync(int userId);
    Task<WishlistStatusDTO> GetStatusAsync(int userId, int eventId);
    Task AddAsync(int userId, int eventId);
    Task RemoveAsync(int userId, int eventId);
    Task<List<AdminWishlistSummaryDTO>> GetAdminSummaryAsync(string? search, string? sort);
}
