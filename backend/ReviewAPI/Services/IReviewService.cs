using ReviewAPI.DTOs;

namespace ReviewAPI.Services;

public interface IReviewService
{
    Task<List<ReviewResponse>> GetByEventAsync(int eventId);
    Task<AdminReviewResponse> GetForAdminAsync(string? eventName, string? status, int? rating, int page, int pageSize);
    Task<ReviewResponse> CreateAsync(CreateReviewRequest request, int userId, IReadOnlyCollection<string> roles);
    Task<ReviewResponse> UpdateAsync(int reviewId, UpdateReviewRequest request, int userId, IReadOnlyCollection<string> roles);
    Task DeleteAsync(int reviewId, int userId, IReadOnlyCollection<string> roles);
    Task ModerateAsync(int reviewId, bool hidden, int adminId);
    Task<ReplyResponse> ReplyAsync(int reviewId, CreateReplyRequest request, int userId, IReadOnlyCollection<string> roles);
}
