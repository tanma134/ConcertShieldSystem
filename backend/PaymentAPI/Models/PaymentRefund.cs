namespace PaymentAPI.Models
{
    // One refund paid back to a customer for an approved ticket return.
    // ReturnRequestId is unique, so retrying the same return can never pay twice.
    public class PaymentRefund
    {
        public int PaymentRefundId { get; set; }
        public int ReturnRequestId { get; set; }
        public int OrderId { get; set; }
        public long Amount { get; set; }
        public string Gateway { get; set; } = "VNPay";
        public string? GatewayRefundRef { get; set; }
        public string Status { get; set; } = "Pending"; // Pending | Refunded | Failed
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
