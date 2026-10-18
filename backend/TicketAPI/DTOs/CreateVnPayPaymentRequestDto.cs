namespace TicketAPI.DTOs
{
    public class CreateVnPayPaymentRequestDto
    {
        public int OrderId { get; set; }
        public long FinalAmount { get; set; }
        public string ClientIp { get; set; } = "127.0.0.1";
        public int? UserId { get; set; }
        public string? DeviceFp { get; set; }
        public string? SessionId { get; set; }
    }
}
