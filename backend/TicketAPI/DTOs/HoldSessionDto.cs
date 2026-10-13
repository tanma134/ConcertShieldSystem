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

        // Chỉ điền khi trả về cho frontend (lấy từ EventAPI), không lưu trong Redis.
        // Giúp trang giữ vé / thanh toán luôn hiện được tên vé + giá dù tra cứu ở client bị hụt.
        public string? TypeName { get; set; }
        public long? UnitPrice { get; set; }
    }

    public class HoldSession
    {
        public string HoldId { get; set; } = "";
        public int UserId { get; set; }
        public int EventId { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        public List<TicketHoldItem> Tickets { get; set; } = new();
        public List<int> SeatIds { get; set; } = new();

        public List<HoldAttendeeDto> Attendees { get; set; } = new();
    }

    public class HoldSessionResponse
    {
        public string HoldId { get; set; } = "";
        public int EventId { get; set; }
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
}
