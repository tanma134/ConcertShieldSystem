using System;

namespace CheckinAPI.Models
{
    public class OfflineSyncConflict
    {
        public string ConflictId { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string? AttendeeName { get; set; }
        public string? ZoneName { get; set; }
        public string? SeatNumber { get; set; }
        public DateTime EarlierScannedAt { get; set; }
        public string? EarlierGateId { get; set; }
        public string? EarlierGateName { get; set; }
        public string? EarlierStaffName { get; set; }
        public string? EarlierMethod { get; set; }
        public DateTime DuplicateScannedAt { get; set; }
        public string? DuplicateGateId { get; set; }
        public string? DuplicateGateName { get; set; }
        public string? DuplicateStaffName { get; set; }
        public string? DuplicateMethod { get; set; }
        public string Status { get; set; } = "PENDING_REVIEW"; // PENDING_REVIEW, RESOLVED
        public string? ResolutionAction { get; set; }
        public string? ResolutionNotes { get; set; }
        public int? ResolvedByUserId { get; set; }
        public string? ResolvedByName { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
