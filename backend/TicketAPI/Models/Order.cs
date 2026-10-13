using System;
using System.Collections.Generic;

namespace TicketAPI.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public int EventId { get; set; }

    public int? QueueSessionId { get; set; }

    public int? VoucherId { get; set; }

    public string? HoldToken { get; set; }

    public string? PaymentGatewayRef { get; set; }

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? EventName { get; set; }

    public string? PosterUrl { get; set; }

    public DateTime? OrderDate { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public long TotalAmount { get; set; }

    public long? DiscountAmount { get; set; }

    public long FinalAmount { get; set; }

    public string? PaymentMethod { get; set; }

    public string? Status { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int? DeletedBy { get; set; }

    public virtual ICollection<OrderAttendee> OrderAttendees { get; set; } = new List<OrderAttendee>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
