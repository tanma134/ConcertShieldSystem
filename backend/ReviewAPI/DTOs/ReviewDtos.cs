using System.ComponentModel.DataAnnotations;

namespace ReviewAPI.DTOs;

public sealed class CreateReviewRequest
{
    [Range(1, 5)] public int Rating { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
    [Required] public int EventId { get; set; }
}

public sealed class UpdateReviewRequest
{
    [Range(1, 5)] public int Rating { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
}

public sealed class CreateReplyRequest
{
    [Required, MaxLength(1000)] public string Comment { get; set; } = null!;
}

public sealed class ReplyResponse
{
    public int ReplyId { get; set; }
    public int ReviewId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "Unknown user";
    public string Role { get; set; } = null!;
    public string Comment { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ReviewResponse
{
    public int ReviewId { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "Unknown user";
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? EventName { get; set; }
    public List<ReplyResponse> Replies { get; set; } = new();
}

public sealed class AdminReviewResponse
{
    public List<ReviewResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
