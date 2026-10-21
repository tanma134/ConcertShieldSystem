namespace TicketAPI.DTOs
{
    public class CheckinScanRequestDto
    {
        public string? QrToken { get; set; }
        public Guid? TicketCode { get; set; }
        public int? TicketId { get; set; }
        public string GateName { get; set; } = "Main Gate";
        /// <summary>
        /// "Entry" (default) or "Exit"
        /// </summary>
        public string ScanType { get; set; } = "Entry";
    }

    public class CheckinScanResponseDto
    {
        public bool Success { get; set; }
        public string ResultCode { get; set; } = "Valid";
        public string Message { get; set; } = string.Empty;
        public int? TicketId { get; set; }
        public string? TicketTypeName { get; set; }
        public string? OwnerName { get; set; }
        public int? SeatId { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public string? CheckedInGate { get; set; }
        public string? ReEntryPolicy { get; set; }
        public int? RemainingMinutes { get; set; }
    }
}
