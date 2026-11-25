using System;

namespace EventAPI.Models
{
    public class EventGate
    {
        public int GateId { get; set; }
        public int EventId { get; set; }
        public string GateName { get; set; } = null!;
        public string? Description { get; set; }
        /// <summary>
        /// Comma-separated list of allowed ticket types or IDs, e.g. "VIP,VVIP" or "GA,Standard" or "ALL"
        /// </summary>
        public string? AllowedTicketTypes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual Event Event { get; set; } = null!;
    }
}
