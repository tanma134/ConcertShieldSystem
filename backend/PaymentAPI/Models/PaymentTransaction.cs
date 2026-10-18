using System;
using System.Collections.Generic;

namespace PaymentAPI.Models;

public partial class PaymentTransaction
{
    public int PaymentTransactionId { get; set; }

    public int OrderId { get; set; }

    public string Gateway { get; set; } = null!;

    public string? GatewayTransactionRef { get; set; }

    public long Amount { get; set; }

    public string Status { get; set; } = null!;

    public string? WebhookPayload { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public int? UserId { get; set; }

    public string? ClientIp { get; set; }

    public string? DeviceFp { get; set; }

    public string? SessionId { get; set; }

    public virtual PaymentReconciliationResolution? PaymentReconciliationResolution { get; set; }
}
