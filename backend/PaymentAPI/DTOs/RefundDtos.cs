namespace PaymentAPI.DTOs
{
    // Body of POST api/internal/refunds (called by TicketAPI after staff approve a return).
    public class CreateRefundRequestDto
    {
        public int ReturnRequestId { get; set; }
        public int OrderId { get; set; }
        public long Amount { get; set; }
    }

    public class RefundResultDto
    {
        public string Status { get; set; } = "Failed"; // Refunded | Failed
        public string? Reference { get; set; }
        public string? Error { get; set; }
    }
}
