using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class RejectEventDTO
    {
        [Required(ErrorMessage = "A rejection reason is required so the organizer knows what to fix.")]
        [MaxLength(500)]
        public string Reason { get; set; } = null!;
    }

    public class SubmitValidationResultDTO
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class PendingEventDTO
    {
        public int EventId { get; set; }
        public int OrganizerId { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? PosterUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string? LocationName { get; set; }
        public string? City { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string Status { get; set; } = null!;
        public bool HasSeatingChart { get; set; }
        public int TicketTypeCount { get; set; }
        public int TotalTickets { get; set; }
        public long? MinPrice { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
