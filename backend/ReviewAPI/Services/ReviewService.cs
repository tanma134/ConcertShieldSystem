using Microsoft.EntityFrameworkCore;
using ReviewAPI.DTOs;
using ReviewAPI.Models;
using ReviewAPI.Repositories;

namespace ReviewAPI.Services;

public sealed class ReviewService(IReviewRepository repository, IEventClient events, IUserClient users, ILogger<ReviewService> logger) : IReviewService
{
    public async Task<List<ReviewResponse>> GetByEventAsync(int eventId)
        => await MapManyAsync(await repository.GetActiveByEventAsync(eventId));

    public async Task<AdminReviewResponse> GetForAdminAsync(string? eventName, string? status, int? rating, int page, int pageSize)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await repository.SearchAdminAsync(status, rating, page: string.IsNullOrWhiteSpace(eventName) ? page : 1, pageSize: string.IsNullOrWhiteSpace(eventName) ? pageSize : 10000);
        var mapped = await MapManyAsync(result.Items);
        if (!string.IsNullOrWhiteSpace(eventName))
        {
            mapped = mapped.Where(x => x.EventName?.Contains(eventName, StringComparison.OrdinalIgnoreCase) == true).ToList();
            var total = mapped.Count;
            mapped = mapped.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new AdminReviewResponse { Items = mapped, Page = page, PageSize = pageSize, TotalCount = total, TotalPages = (int)Math.Ceiling(total / (double)pageSize) };
        }

        return new AdminReviewResponse { Items = mapped, Page = page, PageSize = pageSize, TotalCount = result.TotalCount, TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize) };
    }

    public async Task<ReviewResponse> CreateAsync(CreateReviewRequest request, int userId, IReadOnlyCollection<string> roles)
    {
        EnsureCustomer(roles);
        Validate(request.Rating, request.Comment);
        var target = await events.GetEventAsync(request.EventId, CancellationToken.None);
        if (!target.Exists) throw new KeyNotFoundException("Event not found.");
        if (await repository.GetByEventAndUserAsync(request.EventId, userId) is not null) throw new InvalidOperationException("You have already reviewed this event.");
        var review = new Review { EventId = request.EventId, UserId = userId, Rating = request.Rating, Comment = Normalize(request.Comment) };
        await repository.AddAsync(review);
        try { await repository.SaveChangesAsync(); } catch (DbUpdateException ex) { logger.LogWarning(ex, "Duplicate review for EventId={EventId}, UserId={UserId}", request.EventId, userId); throw new InvalidOperationException("You have already reviewed this event."); }
        return (await MapManyAsync(new List<Review> { review })).Single();
    }

    public async Task<ReviewResponse> UpdateAsync(int reviewId, UpdateReviewRequest request, int userId, IReadOnlyCollection<string> roles)
    {
        EnsureCustomer(roles); Validate(request.Rating, request.Comment);
        var review = await repository.GetByIdAsync(reviewId) ?? throw new KeyNotFoundException("Review not found.");
        if (review.UserId != userId) throw new UnauthorizedAccessException("You can only update your own review.");
        if (review.IsDeleted) throw new InvalidOperationException("Hidden reviews cannot be updated.");
        review.Rating = request.Rating; review.Comment = Normalize(request.Comment); review.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync();
        return (await MapManyAsync(new List<Review> { review })).Single();
    }

    public async Task DeleteAsync(int reviewId, int userId, IReadOnlyCollection<string> roles)
    {
        EnsureCustomer(roles);
        var review = await repository.GetByIdAsync(reviewId) ?? throw new KeyNotFoundException("Review not found.");
        if (review.UserId != userId) throw new UnauthorizedAccessException("You can only delete your own review.");
        if (!review.IsDeleted) { review.IsDeleted = true; review.DeletedAt = DateTime.UtcNow; review.DeletedBy = userId; await repository.SaveChangesAsync(); }
    }

    public async Task ModerateAsync(int reviewId, bool hidden, int adminId)
    {
        var review = await repository.GetByIdAsync(reviewId, true) ?? throw new KeyNotFoundException("Review not found.");
        review.IsDeleted = hidden; review.DeletedAt = hidden ? DateTime.UtcNow : null; review.DeletedBy = hidden ? adminId : null;
        await repository.SaveChangesAsync();
    }

    public async Task<ReplyResponse> ReplyAsync(int reviewId, CreateReplyRequest request, int userId, IReadOnlyCollection<string> roles)
    {
        var role = PickReplyRole(roles);
        var review = await repository.GetByIdAsync(reviewId, true) ?? throw new KeyNotFoundException("Review not found.");
        var target = await events.GetEventAsync(review.EventId, CancellationToken.None);
        if (!target.Exists) throw new KeyNotFoundException("Event not found.");
        if (role == "Organizer" && target.OrganizerId != userId) throw new UnauthorizedAccessException("Organizers can only reply to reviews for their own events.");
        var reply = new ReviewReply { ReviewId = reviewId, UserId = userId, Role = role, Comment = request.Comment.Trim() };
        await repository.AddReplyAsync(reply); await repository.SaveChangesAsync();
        return await MapReplyAsync(reply);
    }

    private async Task<List<ReviewResponse>> MapManyAsync(IEnumerable<Review> reviews)
    {
        var result = new List<ReviewResponse>();
        foreach (var review in reviews) result.Add(await MapAsync(review));
        return result;
    }

    private async Task<ReviewResponse> MapAsync(Review review)
    {
        var target = await events.GetEventAsync(review.EventId, CancellationToken.None);
        return new ReviewResponse { ReviewId = review.ReviewId, EventId = review.EventId, UserId = review.UserId, UserName = await users.GetUserNameAsync(review.UserId, CancellationToken.None), Rating = review.Rating, Comment = review.Comment, CreatedAt = review.CreatedAt, UpdatedAt = review.UpdatedAt, IsDeleted = review.IsDeleted, EventName = target.Name, Replies = await MapRepliesAsync(review.Replies.Where(x => !x.IsDeleted)) };
    }

    private async Task<List<ReplyResponse>> MapRepliesAsync(IEnumerable<ReviewReply> replies)
    {
        var result = new List<ReplyResponse>();
        foreach (var reply in replies.OrderBy(x => x.CreatedAt)) result.Add(await MapReplyAsync(reply));
        return result;
    }

    private async Task<ReplyResponse> MapReplyAsync(ReviewReply reply) => new() { ReplyId = reply.ReplyId, ReviewId = reply.ReviewId, UserId = reply.UserId, UserName = await users.GetUserNameAsync(reply.UserId, CancellationToken.None), Role = reply.Role, Comment = reply.Comment, CreatedAt = reply.CreatedAt, UpdatedAt = reply.UpdatedAt };
    private static void Validate(int rating, string? comment) { if (rating is < 1 or > 5) throw new ArgumentException("Rating must be between 1 and 5."); if (comment?.Length > 1000) throw new ArgumentException("Comment cannot exceed 1000 characters."); }
    private static string? Normalize(string? comment) => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    private static void EnsureCustomer(IReadOnlyCollection<string> roles) { if (!roles.Any(x => string.Equals(x, "Customer", StringComparison.OrdinalIgnoreCase))) throw new UnauthorizedAccessException("Only customers can manage reviews."); }
    private static string PickReplyRole(IReadOnlyCollection<string> roles) { if (roles.Any(x => string.Equals(x, "Admin", StringComparison.OrdinalIgnoreCase))) return "Admin"; if (roles.Any(x => string.Equals(x, "Organizer", StringComparison.OrdinalIgnoreCase))) return "Organizer"; throw new UnauthorizedAccessException("Only Admin or Organizer can reply to reviews."); }
}
