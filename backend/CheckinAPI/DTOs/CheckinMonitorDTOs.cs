using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CheckinAPI.DTOs
{
    public class GateCheckinSummaryDTO
    {
        [JsonPropertyName("gateId")]
        public string GateId { get; set; } = string.Empty;

        [JsonPropertyName("gateName")]
        public string GateName { get; set; } = string.Empty;

        [JsonPropertyName("totalAssigned")]
        public int TotalAssigned { get; set; }

        [JsonPropertyName("checkedIn")]
        public int CheckedIn { get; set; }

        [JsonPropertyName("pending")]
        public int Pending { get; set; }

        [JsonPropertyName("occupancyRate")]
        public double OccupancyRate { get; set; }

        [JsonPropertyName("scanRatePerMin")]
        public int ScanRatePerMin { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Normal";
    }

    public class ZoneCheckinSummaryDTO
    {
        [JsonPropertyName("zoneName")]
        public string ZoneName { get; set; } = string.Empty;

        [JsonPropertyName("totalIssued")]
        public int TotalIssued { get; set; }

        [JsonPropertyName("checkedIn")]
        public int CheckedIn { get; set; }

        [JsonPropertyName("pending")]
        public int Pending { get; set; }

        [JsonPropertyName("occupancyRate")]
        public double OccupancyRate { get; set; }
    }

    public class RecentScanDTO
    {
        [JsonPropertyName("scanId")]
        public string ScanId { get; set; } = string.Empty;

        [JsonPropertyName("ticketCode")]
        public string TicketCode { get; set; } = string.Empty;

        [JsonPropertyName("customerName")]
        public string CustomerName { get; set; } = string.Empty;

        [JsonPropertyName("zoneName")]
        public string ZoneName { get; set; } = string.Empty;

        [JsonPropertyName("gateName")]
        public string GateName { get; set; } = string.Empty;

        [JsonPropertyName("scannedAt")]
        public DateTime ScannedAt { get; set; }

        [JsonPropertyName("scanMethod")]
        public string ScanMethod { get; set; } = "QR Scan";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "SUCCESS";
    }

    public class CheckinMonitorResponseDTO
    {
        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("eventTitle")]
        public string EventTitle { get; set; } = string.Empty;

        [JsonPropertyName("totalIssued")]
        public int TotalIssued { get; set; }

        [JsonPropertyName("checkedIn")]
        public int CheckedIn { get; set; }

        [JsonPropertyName("pending")]
        public int Pending { get; set; }

        [JsonPropertyName("occupancyRate")]
        public double OccupancyRate { get; set; }

        [JsonPropertyName("currentScanRatePerMin")]
        public int CurrentScanRatePerMin { get; set; }

        [JsonPropertyName("peakScanRatePerMin")]
        public int PeakScanRatePerMin { get; set; }

        [JsonPropertyName("filterGateId")] 
        public string? FilterGateId { get; set; }

        [JsonPropertyName("filterZone")]
        public string? FilterZone { get; set; }

        [JsonPropertyName("gates")]
        public List<GateCheckinSummaryDTO> Gates { get; set; } = new();

        [JsonPropertyName("zones")]
        public List<ZoneCheckinSummaryDTO> Zones { get; set; } = new();

        [JsonPropertyName("recentScans")]
        public List<RecentScanDTO> RecentScans { get; set; } = new();

        [JsonPropertyName("lastUpdatedAt")]
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // ==========================================
    // UC_15.2 Scan Dynamic QR Ticket DTOs
    // ==========================================
    public class ScanQrRequestDTO
    {
        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("gateId")]
        public string GateId { get; set; } = "GATE_A";

        [JsonPropertyName("qrPayload")]
        public string QrPayload { get; set; } = string.Empty;
    }

    public class ScanResultResponseDTO
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "SUCCESS"; // SUCCESS, ALREADY_USED, INVALID_QR, WRONG_GATE, EXPIRED

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("ticketCode")]
        public string TicketCode { get; set; } = string.Empty;

        [JsonPropertyName("customerName")]
        public string CustomerName { get; set; } = string.Empty;

        [JsonPropertyName("maskedPhone")]
        public string MaskedPhone { get; set; } = string.Empty;

        [JsonPropertyName("zoneName")]
        public string ZoneName { get; set; } = string.Empty;

        [JsonPropertyName("seatNumber")]
        public string SeatNumber { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("scannedAt")]
        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("previousScanTime")]
        public DateTime? PreviousScanTime { get; set; }

        [JsonPropertyName("previousGateName")]
        public string? PreviousGateName { get; set; }

        [JsonPropertyName("previousStaffName")]
        public string? PreviousStaffName { get; set; }
    }

    // ==========================================
    // UC_15.3 Manual Check-in DTOs (BR-290, BR-292, BR-293)
    // ==========================================
    public class ManualCheckinRequestDTO
    {
        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("gateId")]
        public string GateId { get; set; } = "GATE_A";

        [JsonPropertyName("ticketCode")]
        public string TicketCode { get; set; } = string.Empty;

        [JsonPropertyName("reasonCode")]
        public string ReasonCode { get; set; } = "DEAD_BATTERY"; // DEAD_BATTERY, BROKEN_SCREEN, NETWORK_ERROR, OTHER

        [JsonPropertyName("reasonNote")]
        public string? ReasonNote { get; set; }

        [JsonPropertyName("cccdVerified")]
        public bool CccdVerified { get; set; }

        [JsonPropertyName("cccdNumber")]
        public string? CccdNumber { get; set; }
    }

    // ==========================================
    // UC_15.4 Lookup Ticket DTOs (BR-154, BR-155, BR-156, BR-291, BR-294)
    // ==========================================
    public class LookupTicketRequestDTO
    {
        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("searchType")]
        public string SearchType { get; set; } = "PHONE"; // PHONE, CCCD, ORDER_CODE, TICKET_CODE, EMAIL

        [JsonPropertyName("queryValue")]
        public string QueryValue { get; set; } = string.Empty;
    }

    public class TicketLookupItemDTO
    {
        [JsonPropertyName("ticketId")]
        public int TicketId { get; set; }

        [JsonPropertyName("ticketCode")]
        public string TicketCode { get; set; } = string.Empty;

        [JsonPropertyName("customerName")]
        public string CustomerName { get; set; } = string.Empty;

        [JsonPropertyName("maskedPhone")]
        public string MaskedPhone { get; set; } = string.Empty;

        [JsonPropertyName("maskedEmail")]
        public string MaskedEmail { get; set; } = string.Empty;

        [JsonPropertyName("maskedCccd")]
        public string MaskedCccd { get; set; } = string.Empty;

        [JsonPropertyName("zoneName")]
        public string ZoneName { get; set; } = string.Empty;

        [JsonPropertyName("seatNumber")]
        public string SeatNumber { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "AVAILABLE"; // AVAILABLE, CHECKED_IN, CANCELLED, BLOCKED

        [JsonPropertyName("checkedInAt")]
        public DateTime? CheckedInAt { get; set; }

        [JsonPropertyName("checkedInGate")]
        public string? CheckedInGate { get; set; }
    }

    public class LookupTicketResponseDTO
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("tickets")]
        public List<TicketLookupItemDTO> Tickets { get; set; } = new();
    }

    // ==========================================
    // UC_15.5 View Check-in Scan History DTOs (BR-295)
    // ==========================================
    public class ScanHistoryResponseDTO
    {
        [JsonPropertyName("totalScans")]
        public int TotalScans { get; set; }

        [JsonPropertyName("approvedScans")]
        public int ApprovedScans { get; set; }

        [JsonPropertyName("alreadyUsedScans")]
        public int AlreadyUsedScans { get; set; }

        [JsonPropertyName("invalidScans")]
        public int InvalidScans { get; set; }

        [JsonPropertyName("successRate")]
        public double SuccessRate { get; set; }

        [JsonPropertyName("scans")]
        public List<RecentScanDTO> Scans { get; set; } = new();
    }

    // ==========================================
    // UC_30 / UC-54 Gate Assignment Response DTO
    // ==========================================
    public class GateAssignmentResponseDTO
    {
        [JsonPropertyName("isAssigned")]
        public bool IsAssigned { get; set; } = true;

        [JsonPropertyName("isShiftActive")]
        public bool IsShiftActive { get; set; } = true;

        [JsonPropertyName("gateId")]
        public string GateId { get; set; } = "GATE_A";

        [JsonPropertyName("gateName")]
        public string GateName { get; set; } = "Gate A - Main Entrance (Standing)";

        [JsonPropertyName("shiftName")]
        public string ShiftName { get; set; } = "Evening Main Shift";

        [JsonPropertyName("shiftStartTime")]
        public string ShiftStartTime { get; set; } = "17:00";

        [JsonPropertyName("shiftEndTime")]
        public string ShiftEndTime { get; set; } = "23:00";

        [JsonPropertyName("totalScannedInShift")]
        public int TotalScannedInShift { get; set; } = 354;

        [JsonPropertyName("allowedTicketTiers")]
        public List<string> AllowedTicketTiers { get; set; } = new() { "SVIP Zone A", "VIP Seating B", "General Standing C", "Standing Standard" };

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
