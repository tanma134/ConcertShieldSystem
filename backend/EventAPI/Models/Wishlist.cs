using System;

namespace EventAPI.Models
{
    public class Wishlist
    {
        public int WishlistId { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Event Event { get; set; } = null!;
    }
}
