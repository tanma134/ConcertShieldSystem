using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class CreateReviewDTO
    {
        [Required]
        public int EventId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

    public class ReviewResponseDTO
    {
        public int ReviewId { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class WishlistResponseDTO
    {
        public int WishlistId { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public EventListDTO? Event { get; set; }
    }

    public class OrganizerDashboardStatsDTO
    {
        public int TotalEvents { get; set; }
        public int ApprovedEvents { get; set; }
        public int PendingEvents { get; set; }
        public int DraftEvents { get; set; }
        public int TotalTicketsSold { get; set; }
        public int TotalTickets { get; set; }
        public long TotalRevenue { get; set; }
    }

    public class EventDashboardDTO
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int TotalTickets { get; set; }
        public int SoldTickets { get; set; }
        public int ViewCount { get; set; }
        public long Revenue { get; set; }
        public List<TicketTypeResponseDTO> TicketTypes { get; set; } = new();
    }
}
