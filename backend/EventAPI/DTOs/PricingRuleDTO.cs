using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class CreatePricingRuleDTO
    {
        [Required]
        public int TicketTypeId { get; set; }

        [Required]
        [MaxLength(100)]
        public string RuleName { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        public string RuleType { get; set; } = null!; // EarlyBird, LastMinute, QuantityBased

        public long? AdjustedPrice { get; set; }
        public decimal? DiscountPercent { get; set; }
        public DateTime? TriggerFrom { get; set; }
        public DateTime? TriggerTo { get; set; }
        public int? QuantityThreshold { get; set; }
        public int Priority { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class UpdatePricingRuleDTO
    {
        [MaxLength(100)]
        public string? RuleName { get; set; }

        [MaxLength(30)]
        public string? RuleType { get; set; }

        public long? AdjustedPrice { get; set; }
        public decimal? DiscountPercent { get; set; }
        public DateTime? TriggerFrom { get; set; }
        public DateTime? TriggerTo { get; set; }
        public int? QuantityThreshold { get; set; }
        public int? Priority { get; set; }
        public bool? IsActive { get; set; }
    }

    public class PricingRuleResponseDTO
    {
        public int PricingRuleId { get; set; }
        public int TicketTypeId { get; set; }
        public string RuleName { get; set; } = null!;
        public string RuleType { get; set; } = null!;
        public long? AdjustedPrice { get; set; }
        public decimal? DiscountPercent { get; set; }
        public DateTime? TriggerFrom { get; set; }
        public DateTime? TriggerTo { get; set; }
        public int? QuantityThreshold { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
