using System;
using System.Collections.Generic;

namespace TicketAPI.Models
{
    public class TicketReconciliation
    {
        public int ReconciliationId { get; set; }
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int AdminId { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public int TotalTicketsRevoked { get; set; }
        public string ReasonType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string RefundStatus { get; set; } = "QUEUED";

        public virtual ICollection<TicketRevocation> Revocations { get; set; } = new List<TicketRevocation>();
    }
}
