using System;
using System.Collections.Generic;

namespace PaymentAPI.Models;

public partial class Voucher
{
    public int VoucherId { get; set; }

    public string Code { get; set; } = null!;

    public string Scope { get; set; } = null!;

    public int? OrganizerId { get; set; }

    public int? EventId { get; set; }

    public string DiscountType { get; set; } = null!;

    public long? DiscountAmount { get; set; }

    public decimal? DiscountPercent { get; set; }

    public long? MaxDiscountAmount { get; set; }

    public long? MinOrderAmount { get; set; }

    public int TotalQuantity { get; set; }

    public int UsedQuantity { get; set; }

    public int MaxUsagePerUser { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public bool IsActive { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int? DeletedBy { get; set; }

    public virtual ICollection<VoucherUsage> VoucherUsages { get; set; } = new List<VoucherUsage>();
}
