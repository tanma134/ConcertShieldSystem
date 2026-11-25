using System;
using System.Collections.Generic;

namespace TicketAPI.DTOs
{
    // ==========================================
    // Bulk Revoke Tickets DTOs
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
    // Ticket Revocation History DTOs
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
    // Reissue History DTOs
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
}
