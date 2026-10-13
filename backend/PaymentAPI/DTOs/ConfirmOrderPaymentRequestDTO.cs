namespace PaymentAPI.DTOs
{
    public class ConfirmOrderPaymentRequestDTO
    {
        public long Amount { get; set; }
        public string TransactionRef { get; set; } = string.Empty;
    }
}
