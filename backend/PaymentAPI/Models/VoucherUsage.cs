using System;
using System.Collections.Generic;

namespace PaymentAPI.Models;

public partial class VoucherUsage
{
    public int VoucherUsageId { get; set; }

    public int VoucherId { get; set; }

    public int OrderId { get; set; }

    public int UserId { get; set; }

    public long DiscountAmount { get; set; }

    public DateTime UsedAt { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
}
