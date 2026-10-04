using System;

namespace PaymentAPI.Models
{
    public class PaymentTransaction
    {
        public int PaymentTransactionId { get; set; }
        public int OrderId { get; set; }
        public string Gateway { get; set; } = "VNPay";
        public string? GatewayTransactionRef { get; set; }
        public long Amount { get; set; }
        public string Status { get; set; } = "Initiated";
        public string? WebhookPayload { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
    }
}
