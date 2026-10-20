namespace TicketAPI.DTOs
{
    public class StatusHistoryResponseDto
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

        public string Action { get; set; } = null!;
        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }
        public int? Quantity { get; set; }

        public int? ActorUserId { get; set; }
        public string? ActorRole { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Metadata { get; set; } = "{}";
    }

    public class StatusHistoryPageDto
    {
        public List<StatusHistoryResponseDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
