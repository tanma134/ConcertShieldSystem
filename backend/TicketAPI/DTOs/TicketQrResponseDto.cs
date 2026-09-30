namespace TicketAPI.DTOs
{
    public class TicketQrResponseDto
    {
        public int TicketId { get; set; }
        public string QrToken { get; set; } = "";
        public DateTime ExpiresAtUtc { get; set; }
    }
}
