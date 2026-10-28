using System;
using System.Collections.Generic;

namespace EventAPI.Models
{
    public class TicketType
    {
        public int TicketTypeId { get; set; }
        public int EventId { get; set; }
        public string TypeName { get; set; } = null!;
        public string? Description { get; set; }
        public long Price { get; set; }
        public long? OriginalPrice { get; set; }
        public int Quantity { get; set; }
        public int SoldQuantity { get; set; } = 0;
        public int MinPerOrder { get; set; } = 1;
        public int MaxPerOrder { get; set; } = 10;
        public string? ColorCode { get; set; }
        public int SortOrder { get; set; } = 0;
        public string Status { get; set; } = "Active";
        public DateTime? SalesStartsAt { get; set; }
        public DateTime? SalesEndsAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }

        public virtual Event Event { get; set; } = null!;
        public virtual ICollection<PricingRule> PricingRules { get; set; } = new List<PricingRule>();
        public virtual ICollection<SeatZone> SeatZones { get; set; } = new List<SeatZone>();
    }
}
