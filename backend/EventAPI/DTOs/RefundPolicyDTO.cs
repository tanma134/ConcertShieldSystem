using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class CreateRefundPolicyDTO
    {
        [Required]
        [MaxLength(150)]
        public string PolicyName { get; set; } = null!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public int DeadlineBeforeEventHours { get; set; }

        [Required]
        public decimal RefundPercent { get; set; }

        public bool RequiresOrganizerApproval { get; set; } = true;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateRefundPolicyDTO
    {
        [Required]
        public int RefundPolicyId { get; set; }

        [MaxLength(150)]
        public string? PolicyName { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? DeadlineBeforeEventHours { get; set; }
        public decimal? RefundPercent { get; set; }
        public bool? RequiresOrganizerApproval { get; set; }
        public bool? IsActive { get; set; }
    }

    public class RefundPolicyResponseDTO
    {
        public int RefundPolicyId { get; set; }
        public int EventId { get; set; }
        public string PolicyName { get; set; } = null!;
        public string? Description { get; set; }
        public int DeadlineBeforeEventHours { get; set; }
        public decimal RefundPercent { get; set; }
        public bool RequiresOrganizerApproval { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
