using System;
using System.Collections.Generic;

namespace CheckinAPI.Models
{
    public class Order
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public int EventId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = "Pending";
        public bool IsDeleted { get; set; }

        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
        public virtual ICollection<OrderAttendee> OrderAttendees { get; set; } = new List<OrderAttendee>();
    }
}
