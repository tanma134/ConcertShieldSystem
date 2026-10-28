namespace ReviewAPI.Models;

public sealed class Review
{
    public int ReviewId { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
    public ICollection<ReviewReply> Replies { get; set; } = new List<ReviewReply>();
}
