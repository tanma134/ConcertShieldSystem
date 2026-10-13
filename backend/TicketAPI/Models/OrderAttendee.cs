using System;
using System.Collections.Generic;

namespace TicketAPI.Models;

public partial class OrderAttendee
{
    public int AttendeeId { get; set; }

    public int OrderId { get; set; }

    public int? TicketTypeId { get; set; }

    public string FullName { get; set; } = null!;

    public string? CitizenId { get; set; }

    public string? Phone { get; set; }

    public bool? IsPrimaryBuyer { get; set; }

    public int? SeatId { get; set; }

    public virtual Order Order { get; set; } = null!;
}
