using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class CreateEventDTO
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(500)]
        public string? ShortDescription { get; set; }

        public string? Description { get; set; }

        [MaxLength(200)]
        public string? LocationName { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        public decimal? Longitude { get; set; }
        public decimal? Latitude { get; set; }

        [Required]
        public DateTime StartsAt { get; set; }

        [Required]
        public DateTime EndsAt { get; set; }

        [MaxLength(50)]
        public string Timezone { get; set; } = "SE Asia Standard Time";

        public bool HasSeatingChart { get; set; } = false;
        public bool RequiresVirtualQueue { get; set; } = false;

        public int? MinTicketsPerAccount { get; set; }
        public int? MaxTicketsPerAccount { get; set; }

        [MaxLength(200)]
        public string? MetaTitle { get; set; }

        [MaxLength(500)]
        public string? MetaDescription { get; set; }
    }

    public class UpdateEventDTO
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(500)]
        public string? ShortDescription { get; set; }

        public string? Description { get; set; }

        [MaxLength(200)]
        public string? LocationName { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        public decimal? Longitude { get; set; }
        public decimal? Latitude { get; set; }

        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        [MaxLength(50)]
        public string? Timezone { get; set; }

        public bool? HasSeatingChart { get; set; }
        public bool? RequiresVirtualQueue { get; set; }

        public int? MinTicketsPerAccount { get; set; }
        public int? MaxTicketsPerAccount { get; set; }

        [MaxLength(200)]
        public string? MetaTitle { get; set; }

        [MaxLength(500)]
        public string? MetaDescription { get; set; }
    }

    public class UpdateEventStatusDTO
    {
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = null!;

        [MaxLength(500)]
        public string? RejectedReason { get; set; }
    }
}
