using System;

namespace TicketAPI.Models
{
    public class TicketRevocation
    {
        public int RevocationId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int RevokedByAdminId { get; set; }
        public string RevokedByName { get; set; } = string.Empty;
        public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
        public string ReasonType { get; set; } = string.Empty;
        public string? ReasonNote { get; set; }
        public int? ReconciliationId { get; set; }
        public bool IsBulk { get; set; }
        public bool RefundQueued { get; set; } = true;
        public bool NotificationSent { get; set; } = true;
        public string? SeatOrZone { get; set; }
        public string? CustomerName { get; set; }

        public virtual TicketReconciliation? Reconciliation { get; set; }
    }
}
