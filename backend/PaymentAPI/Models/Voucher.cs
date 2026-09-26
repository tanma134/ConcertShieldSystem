using System;
using System.Collections.Generic;

namespace PaymentAPI.Models
{
    public class Voucher
    {
        public int VoucherId { get; set; }
        public string Code { get; set; } = null!;
        public string Scope { get; set; } = null!; // "SYSTEM", "ORGANIZER", "EVENT"
        public int? OrganizerId { get; set; }
        public int? EventId { get; set; }
        public string DiscountType { get; set; } = null!; // "PERCENT", "FIXED"
        public long? DiscountAmount { get; set; }
        public decimal? DiscountPercent { get; set; }
        public long? MaxDiscountAmount { get; set; }
        public long? MinOrderAmount { get; set; } = 0;
        public int TotalQuantity { get; set; }
        public int UsedQuantity { get; set; } = 0;
        public int MaxUsagePerUser { get; set; } = 1;
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public bool IsActive { get; set; } = true;
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }

        // Navigation
        public virtual ICollection<VoucherUsage> Usages { get; set; } = new List<VoucherUsage>();
    }
}
