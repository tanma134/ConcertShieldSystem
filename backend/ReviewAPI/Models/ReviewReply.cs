namespace ReviewAPI.Models;

public sealed class ReviewReply
{
    public int ReplyId { get; set; }
    public int ReviewId { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = null!;
    public string Comment { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
    public Review Review { get; set; } = null!;
}
