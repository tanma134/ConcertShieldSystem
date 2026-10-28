using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class CreateOrganizerRequestDTO
    {
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = null!;

        [MaxLength(200)]
        public string? CompanyName { get; set; }

        [MaxLength(255)]
        [Url]
        public string? Website { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(2000)]
        public string? Experience { get; set; }
    }

    public class ReviewOrganizerRequestDTO
    {
        [Required]
        public string Status { get; set; } = null!;

        [MaxLength(1000)]
        public string? ReviewNote { get; set; }

        [MaxLength(1000)]
        public string? RejectionReason { get; set; }
    }


    public class OrganizerRequestResponseDTO
    {
        public int RequestId { get; set; }
        public int UserId { get; set; }
        public string UserEmail { get; set; } = null!;
        public string UserFullName { get; set; } = null!;

        public string Reason { get; set; } = null!;
        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Experience { get; set; }

        public string Status { get; set; } = null!;
        public int? ReviewedBy { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}