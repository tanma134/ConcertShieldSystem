namespace PaymentAPI.DTOs
{
    public class ResolvePaymentReconciliationRequestDto
    {
        public string ResolutionAction { get; set; } = null!;
        public string ResolutionNote { get; set; } = null!;
        public string ResolutionStatus { get; set; } = "Resolved";
    }
}
