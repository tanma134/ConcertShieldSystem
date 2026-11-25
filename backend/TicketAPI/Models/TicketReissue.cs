using System;

namespace TicketAPI.Models
{
    public class TicketReissue
    {
        public int ReissueId { get; set; }
        public string OldTicketCode { get; set; } = string.Empty;
        public string NewTicketCode { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int ReissuedByAdminId { get; set; }
        public string ReissuedByName { get; set; } = string.Empty;
        public DateTime ReissuedAt { get; set; } = DateTime.UtcNow;
        public string Reason { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string? SeatOrZone { get; set; }
        public string? DynamicQrStatus { get; set; } = "ACTIVE";
    }
}
