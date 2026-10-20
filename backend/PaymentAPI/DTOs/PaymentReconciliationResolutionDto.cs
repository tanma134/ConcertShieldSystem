namespace PaymentAPI.DTOs
{
    public class PaymentReconciliationResolutionDto
    {
        public int PaymentTransactionId { get; set; }
        public string ResolutionStatus { get; set; } = null!;
        public string ResolutionAction { get; set; } = null!;
        public string ResolutionNote { get; set; } = null!;
        public int ResolvedByUserId { get; set; }
        public DateTime ResolvedAt { get; set; }
    }
}
