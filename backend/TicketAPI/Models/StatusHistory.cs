namespace TicketAPI.Models
{
    public class StatusHistory
    {
        public int StatusHistoryId { get; set; }

        public string TargetType { get; set; } = null!;
        public int TargetId { get; set; }

        public int EventId { get; set; }
        public int? OrderId { get; set; }
        public int? TicketId { get; set; }
        public int? TicketTypeId { get; set; }
        public int? SeatId { get; set; }
        public string? HoldId { get; set; }
        public int? CustomerId { get; set; }

        public string Action { get; set; } = null!;
        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }
        public int? Quantity { get; set; }

        public int? ActorUserId { get; set; }
        public string? ActorRole { get; set; }
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; }

        // Dữ liệu JSON bổ sung, ví dụ: {"source":"VNPay"}
        public string Metadata { get; set; } = "{}";
    }
}
