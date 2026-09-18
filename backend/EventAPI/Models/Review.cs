using System;

namespace EventAPI.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        public virtual Event Event { get; set; } = null!;
    }
}
