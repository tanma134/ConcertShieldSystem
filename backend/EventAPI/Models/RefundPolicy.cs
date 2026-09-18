using System;

namespace EventAPI.Models
{
    public class RefundPolicy
    {
        public int RefundPolicyId { get; set; }
        public int EventId { get; set; }
        public string PolicyName { get; set; } = null!;
        public string? Description { get; set; }
        public int DeadlineBeforeEventHours { get; set; }
        public decimal RefundPercent { get; set; }
        public bool RequiresOrganizerApproval { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Event Event { get; set; } = null!;
    }
}
