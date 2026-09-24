using System;

namespace EventAPI.Models
{
    public class PricingRule
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
        public int Priority { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual TicketType TicketType { get; set; } = null!;
    }
}
