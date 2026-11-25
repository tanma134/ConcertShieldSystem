using System;

namespace CheckinAPI.Models
{
    public class TicketQrToken
    {
        public int TicketQrTokenId { get; set; }
        public int TicketId { get; set; }
        public string QrToken { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }

        public virtual Ticket? Ticket { get; set; }
    }
}
