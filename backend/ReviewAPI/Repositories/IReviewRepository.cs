using ReviewAPI.Models;

namespace ReviewAPI.Repositories;

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(int reviewId, bool includeDeleted = false);
    Task<Review?> GetByEventAndUserAsync(int eventId, int userId);
    Task<List<Review>> GetActiveByEventAsync(int eventId);
    Task<(List<Review> Items, int TotalCount)> SearchAdminAsync(string? status, int? rating, int page, int pageSize);
    Task AddAsync(Review review);
    Task AddReplyAsync(ReviewReply reply);
    Task SaveChangesAsync();
}
