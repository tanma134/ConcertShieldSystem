using Microsoft.EntityFrameworkCore;
using ReviewAPI.Data;
using ReviewAPI.Models;

namespace ReviewAPI.Repositories;

public sealed class ReviewRepository(ReviewDbContext context) : IReviewRepository
{
    private IQueryable<Review> WithReplies(bool includeDeleted) => context.Reviews
        .Include(x => x.Replies.Where(reply => includeDeleted || !reply.IsDeleted));

    public Task<Review?> GetByIdAsync(int reviewId, bool includeDeleted = false)
        => WithReplies(includeDeleted).SingleOrDefaultAsync(x => x.ReviewId == reviewId && (includeDeleted || !x.IsDeleted));

    public Task<Review?> GetByEventAndUserAsync(int eventId, int userId)
        => WithReplies(true).SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);

    public Task<List<Review>> GetActiveByEventAsync(int eventId)
        => WithReplies(false).Where(x => x.EventId == eventId && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).ToListAsync();

    public async Task<(List<Review> Items, int TotalCount)> SearchAdminAsync(string? status, int? rating, int page, int pageSize)
    {
        var query = WithReplies(true).AsQueryable();
        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => !x.IsDeleted);
        if (string.Equals(status, "hidden", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.IsDeleted);
        if (rating.HasValue) query = query.Where(x => x.Rating == rating.Value);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public Task AddAsync(Review review) => context.Reviews.AddAsync(review).AsTask();
    public Task AddReplyAsync(ReviewReply reply) => context.ReviewReplies.AddAsync(reply).AsTask();
    public Task SaveChangesAsync() => context.SaveChangesAsync();
}
