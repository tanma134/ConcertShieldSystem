namespace TicketAPI.DTOs
{
    public class CreateHoldSessionRequest
    {
        public int EventId { get; set; }
        public List<TicketHoldItem> Tickets { get; set; } = new();
        public List<int> SeatIds { get; set; } = new();
    }

    public class TicketHoldItem
    {
        public int TicketTypeId { get; set; }
        public int Quantity { get; set; }

        // Populated only in responses to the frontend (from EventAPI), not stored in Redis,
        // so the hold/checkout pages still display ticket name and price if client lookup fails.
        public string? TypeName { get; set; }
        public long? UnitPrice { get; set; }
    }

    public class HoldSession
    {
        public string HoldId { get; set; } = "";
        public int UserId { get; set; }
        public int EventId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public int DurationSeconds { get; set; } = 600;
        public DateTime ExpiresAtUtc { get; set; }

        public List<TicketHoldItem> Tickets { get; set; } = new();
        public List<int> SeatIds { get; set; } = new();

        public List<HoldAttendeeDto> Attendees { get; set; } = new();
    }

    public class HoldSessionResponse
    {
        public string HoldId { get; set; } = "";
        public int EventId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public int DurationSeconds { get; set; } = 600;
        public DateTime ExpiresAtUtc { get; set; }
        public int RemainingSeconds { get; set; }
        public long TotalAmount { get; set; }
        public List<TicketHoldItem> Tickets { get; set; } = new();
        public List<int> SeatIds { get; set; } = new();
        public List<HoldAttendeeDto> Attendees { get; set; } = new();
    }

    public class HoldAttendeeDto
    {
        public string FullName { get; set; } = string.Empty;
        public string CitizenId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public string? Email { get; set; }

        public int TicketTypeId { get; set; }
        public int? SeatId { get; set; }
        public bool IsPrimaryBuyer { get; set; }
    }

    public class SaveHoldAttendeesRequest
    {
        public List<HoldAttendeeDto> Attendees { get; set; } = new();
    }

    public class ChangeHoldSeatsRequest
    {
        public List<int> SeatIds { get; set; } = new();
    }
}
