namespace PaymentAPI.Models
{
    public class PaymentReconciliationResolution
    {
        public int PaymentReconciliationResolutionId { get; set; }

        public int PaymentTransactionId { get; set; }

        public string ResolutionStatus { get; set; } = null!;

        public string ResolutionAction { get; set; } = null!;

        public string ResolutionNote { get; set; } = null!;

        public int ResolvedByUserId { get; set; }

        public DateTime ResolvedAt { get; set; }

        public PaymentTransaction PaymentTransaction { get; set; } = null!;
    }
}
