using System;
using System.Collections.Generic;

namespace PaymentAPI.Models;

public partial class PaymentReconciliationResolution
{
    public int PaymentReconciliationResolutionId { get; set; }

    public int PaymentTransactionId { get; set; }

    public string ResolutionStatus { get; set; } = null!;

    public string ResolutionNote { get; set; } = null!;

    public int ResolvedByUserId { get; set; }

    public DateTime ResolvedAt { get; set; }

    public string ResolutionAction { get; set; } = null!;

    public virtual PaymentTransaction PaymentTransaction { get; set; } = null!;
}
