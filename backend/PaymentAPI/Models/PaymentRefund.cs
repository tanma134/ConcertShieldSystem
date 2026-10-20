namespace PaymentAPI.Models
{
    // One refund paid back to a customer for an approved ticket return or order support resolution.
    public class PaymentRefund
    {
        public int PaymentRefundId { get; set; }
        public int? ReturnRequestId { get; set; }
        public int OrderId { get; set; }
        public long Amount { get; set; }
        public string Gateway { get; set; } = "VNPay";
        public string? GatewayRefundRef { get; set; }
        public string Status { get; set; } = "Pending"; // Pending | Refunded | Failed
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public string RefundType { get; set; } = "Full"; // Full | Partial
        public string? Reason { get; set; }
        public int? ProcessedByUserId { get; set; }
        public string? ProcessedByUserName { get; set; }
        public string? CustomerName { get; set; }
        public string? GatewayErrorCode { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime? LastRetryAt { get; set; }
        public long? GatewayAmount { get; set; }
        public string ReconciliationStatus { get; set; } = "Matched"; // Matched | Mismatched | Pending
        public int? EventId { get; set; }
        public string? EventName { get; set; }
    }
}
