using System;
using System.Collections.Generic;

namespace CheckinAPI.DTOs
{
    // ==========================================
    // UC_14.5 & UC_14.6 Offline Check-in DTOs
    // ==========================================
    public class OfflineTicketItemDTO
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string? QrToken { get; set; }
        public string TicketTokenHash { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public string AssignedGate { get; set; } = "GATE_A";
        public bool IsCheckedIn { get; set; } = false;
        public string Status { get; set; } = "Available"; // Available, Used, Revoked
    }

    public class OfflinePackageResponseDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public string AssignedGate { get; set; } = "Main Gate";
        public List<string> AllowedTicketTypes { get; set; } = new();
        public string? ShiftStartTime { get; set; }
        public string? ShiftEndTime { get; set; }
        public DateTime ExportedAt { get; set; }
        public int TotalTickets { get; set; }
        public string Signature { get; set; } = string.Empty;
        public List<OfflineTicketItemDTO> Tickets { get; set; } = new();
    }

    public class OfflineScanRecordDTO
    {
        public string TicketCode { get; set; } = string.Empty;
        public string GateId { get; set; } = "GATE_A";
        public DateTime ScannedAt { get; set; }
        public string ScanMethod { get; set; } = "Offline QR Scan";
        public int StaffUserId { get; set; }
        public string? Signature { get; set; }
    }

    public class OfflineSyncRequestDTO
    {
        public int EventId { get; set; }
        public string GateId { get; set; } = "GATE_A";
        public List<OfflineScanRecordDTO> Scans { get; set; } = new();
    }

    public class OfflineSyncConflictDTO
    {
        public string ConflictId { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = "Attendee";
        public string ZoneName { get; set; } = "Grandstand Area";
        public string SeatNumber { get; set; } = "S-01";
        public string? AccountName { get; set; }
        public string? AccountEmail { get; set; }
        public string? AccountPhone { get; set; }

        // Scan 1: earlier scan
        public DateTime EarlierScannedAt { get; set; }
        public string EarlierGateId { get; set; } = "GATE_A";
        public string EarlierGateName { get; set; } = "Gate A - Main Entrance";
        public string EarlierStaffName { get; set; } = "Staff Gate 1";
        public string EarlierMethod { get; set; } = "Offline QR Scan";

        // Scan 2: duplicate scan
        public DateTime DuplicateScannedAt { get; set; }
        public string DuplicateGateId { get; set; } = "GATE_B";
        public string DuplicateGateName { get; set; } = "Gate B - North Wing";
        public string DuplicateStaffName { get; set; } = "Staff Gate 2";
        public string DuplicateMethod { get; set; } = "Offline QR Scan";

        public int TimeDifferenceSeconds => (int)Math.Abs((DuplicateScannedAt - EarlierScannedAt).TotalSeconds);
        public string Status { get; set; } = "PENDING_REVIEW"; // PENDING_REVIEW, RESOLVED_EARLIER_VALID, RESOLVED_SECURITY_ESCALATED
        public string? Resolution { get; set; }
        public string? ResolutionNotes { get; set; }
        public string? ResolvedByName { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class ResolveConflictRequestDTO
    {
        public string Decision { get; set; } = "ACCEPT_EARLIER_REJECT_LATER"; // ACCEPT_EARLIER_REJECT_LATER, ESCALATE_SECURITY, REVOKE_BOTH
        public string Notes { get; set; } = string.Empty;
        public string Resolution
        {
            get => Decision;
            set => Decision = value ?? string.Empty;
        }
        public string Reason
        {
            get => Notes;
            set => Notes = value ?? string.Empty;
        }
    }

    public class OfflineSyncResponseDTO
    {
        public bool Success { get; set; }
        public int TotalProcessed { get; set; }
        public int NewlyCheckedIn { get; set; }
        public int SyncedCount => NewlyCheckedIn;
        public int AlreadyUsedCount { get; set; }
        public int ConflictCount { get; set; }
        public List<OfflineSyncConflictDTO> Conflicts { get; set; } = new();
        public DateTime SyncedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
