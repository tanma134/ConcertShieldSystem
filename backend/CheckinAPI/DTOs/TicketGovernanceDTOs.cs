using System;
using System.Collections.Generic;

namespace CheckinAPI.DTOs
{
    // ==========================================
    // UC_8.7 Bulk Revoke Tickets DTOs
    // ==========================================
    public class BulkRevokeRequestDTO
    {
        public int EventId { get; set; }
        public string ReasonType { get; set; } = "EVENT_CANCELLED"; // EVENT_CANCELLED, EVENT_POSTPONED, FORCE_MAJEURE
        public string ReasonNote { get; set; } = string.Empty;
        public bool TriggerRefundQueue { get; set; } = true;
        public bool NotifyAttendees { get; set; } = true;
    }

    public class BulkRevokeResponseDTO
    {
        public bool Success { get; set; }
        public int ReconciliationId { get; set; }
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int TotalRevokedTickets { get; set; }
        public string ReasonType { get; set; } = string.Empty;
        public string ReasonNote { get; set; } = string.Empty;
        public DateTime RevokedAt { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public bool GateCacheInvalidated { get; set; }
        public bool RefundQueued { get; set; }
        public bool NotificationSent { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // ==========================================
    // UC_8.8 Ticket Revocation History DTOs
    // ==========================================
    public class RevocationHistoryItemDTO
    {
        public int RevocationId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int RevokedByAdminId { get; set; }
        public string RevokedByName { get; set; } = string.Empty;
        public DateTime RevokedAt { get; set; }
        public string ReasonType { get; set; } = string.Empty;
        public string ReasonNote { get; set; } = string.Empty;
        public int? ReconciliationId { get; set; }
        public bool IsBulkRevocation { get; set; }
        public bool RefundQueued { get; set; }
        public bool NotificationSent { get; set; }
        public string SeatOrZone { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
    }

    public class RevocationHistoryResponseDTO
    {
        public List<RevocationHistoryItemDTO> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // Reconciliation Record for Bulk Revocation
    public class ReconciliationRecordDTO
    {
        public int ReconciliationId { get; set; }
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int AdminId { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public int TotalRevokedTickets { get; set; }
        public int TotalTicketsRevoked { get => TotalRevokedTickets; set => TotalRevokedTickets = value; }
        public string ReasonType { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string RefundStatus { get; set; } = "QUEUED";
    }

    // ==========================================
    // UC_8.10 Reissue History DTOs
    // ==========================================
    public class ReissueHistoryItemDTO
    {
        public int ReissueId { get; set; }
        public string OldTicketCode { get; set; } = string.Empty;
        public string NewTicketCode { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public int ReissuedByAdminId { get; set; }
        public string ReissuedByName { get; set; } = string.Empty;
        public DateTime ReissuedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string SeatOrZone { get; set; } = string.Empty;
        public string DynamicQrStatus { get; set; } = "ACTIVE";
    }

    public class ReissueHistoryResponseDTO
    {
        public List<ReissueHistoryItemDTO> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // ==========================================
    // Governance Summary Stats
    // ==========================================
    public class GovernanceStatsDTO
    {
        public int TotalRevokedTickets { get; set; }
        public int TotalBulkEvents { get; set; }
        public int TotalReissuedTickets { get; set; }
        public int TotalRefundQueued { get; set; }
    }

    public class EventSelectionDTO
    {
        public int EventId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ActiveTicketsCount { get; set; }
        public DateTime StartDate { get; set; }
        public string Location { get; set; } = string.Empty;
    }

    // ==========================================
    // UC_14.5 & UC_14.6 Offline Check-in DTOs
    // ==========================================
    public class OfflineTicketItemDTO
    {
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
        public string AttendeeName { get; set; } = "Khán Giả";
        public string ZoneName { get; set; } = "Khu Khán Đài";
        public string SeatNumber { get; set; } = "S-01";

        // Scan 1: earlier scan
        public DateTime EarlierScannedAt { get; set; }
        public string EarlierGateId { get; set; } = "GATE_A";
        public string EarlierGateName { get; set; } = "Cổng A - Lối vào chính";
        public string EarlierStaffName { get; set; } = "Nhân viên Soát vé 1";
        public string EarlierMethod { get; set; } = "Offline QR Scan";

        // Scan 2: duplicate scan
        public DateTime DuplicateScannedAt { get; set; }
        public string DuplicateGateId { get; set; } = "GATE_B";
        public string DuplicateGateName { get; set; } = "Cổng B - Cánh Bắc";
        public string DuplicateStaffName { get; set; } = "Nhân viên Soát vé 2";
        public string DuplicateMethod { get; set; } = "Offline QR Scan";

        public int TimeDifferenceSeconds => (int)Math.Abs((DuplicateScannedAt - EarlierScannedAt).TotalSeconds);
        public string Status { get; set; } = "PENDING_REVIEW"; // PENDING_REVIEW, RESOLVED_EARLIER_VALID, RESOLVED_SECURITY_ESCALATED
        public string? ResolutionNotes { get; set; }
        public string? ResolvedByName { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class ResolveConflictRequestDTO
    {
        public string Decision { get; set; } = "ACCEPT_EARLIER_REJECT_LATER"; // ACCEPT_EARLIER_REJECT_LATER, ESCALATE_SECURITY, REVOKE_BOTH
        public string Notes { get; set; } = string.Empty;
    }

    public class OfflineSyncResponseDTO
    {
        public bool Success { get; set; }
        public int TotalProcessed { get; set; }
        public int NewlyCheckedIn { get; set; }
        public int AlreadyUsedCount { get; set; }
        public int ConflictCount { get; set; }
        public List<OfflineSyncConflictDTO> Conflicts { get; set; } = new();
        public DateTime SyncedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
