using System;

namespace EventAPI.Models
{
    public class EventImage
    {
        public int ImageId { get; set; }
        public int EventId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string? PublicId { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool IsMain { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }

        public virtual Event Event { get; set; } = null!;
    }
}
