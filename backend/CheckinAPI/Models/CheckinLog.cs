using System;

namespace CheckinAPI.Models
{
    public class CheckinLog
    {
        public int LogId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string GateId { get; set; } = "GATE_A";
        public string GateName { get; set; } = "Gate A - Main Entrance";
        public string ScanMethod { get; set; } = "QR";
        public string Status { get; set; } = "APPROVED"; // APPROVED, REJECTED, ALREADY_CHECKED_IN
        public int StaffUserId { get; set; }
        public int EventId { get; set; }
        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
        public DateTime ScanTime { get; set; } = DateTime.UtcNow;
    }
}
