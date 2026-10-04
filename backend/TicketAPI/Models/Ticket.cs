using System;
using System.Collections.Generic;

namespace TicketAPI.Models;

public partial class Ticket
{
    public int TicketId { get; set; }

    public Guid TicketCode { get; set; }

    public int OrderId { get; set; }

    public int EventId { get; set; }

    public int TicketTypeId { get; set; }

    public int? SeatId { get; set; }

    public int? OwnerUserId { get; set; }

    public string? TicketTypeName { get; set; }

    public string? OwnerName { get; set; }

    public string? Status { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public int? CheckedInBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int? DeletedBy { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<TicketQrToken> TicketQrTokens { get; set; } = new List<TicketQrToken>();
}
