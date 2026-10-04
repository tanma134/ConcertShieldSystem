using System;

namespace PaymentAPI.Models
{
    public class VoucherUsage
    {
        public int VoucherUsageId { get; set; }
        public int VoucherId { get; set; }
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public long DiscountAmount { get; set; }
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Voucher Voucher { get; set; } = null!;
    }
}
