using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CheckinAPI.Models;

namespace CheckinAPI.Repositories
{
    public class ZoneCheckinCount
    {
        public string ZoneName { get; set; } = string.Empty;
        public int TotalIssued { get; set; }
        public int CheckedIn { get; set; }
    }

    public class GateCheckinCount
    {
        public string GateId { get; set; } = "GATE_A";
        public string GateName { get; set; } = "Gate A - Main Entrance";
        public int ScanCount { get; set; }
        public int SuccessCount { get; set; }
    }

    public interface ICheckinRepository
    {
        Task<(int TotalIssued, int CheckedIn, List<ZoneCheckinCount> ZoneCounts)> GetMonitorCountsAsync(int eventId);
        Task<List<GateCheckinCount>> GetGateCountsAsync(int eventId);
        Task<List<CheckinLog>> GetRecentScansAsync(int eventId, int limit);
        Task<Ticket?> GetTicketByCodeAsync(string ticketCode);
        Task<Ticket?> GetTicketByIdAsync(int ticketId);
        Task<Ticket?> GetTicketForScanAsync(string rawPayload);
        Task<Ticket?> GetTicketForManualCheckinAsync(int eventId, string? ticketCode, string? cccd);
        Task AddCheckinLogAsync(CheckinLog log);
        Task SaveCheckinSuccessAsync(Ticket ticket, TicketQrToken? qrToken, CheckinLog log);
        Task SaveReEntrySuccessAsync(Ticket ticket, CheckinLog log);
        Task SaveExitSuccessAsync(Ticket ticket);
        Task<List<Ticket>> LookupTicketsAsync(int eventId, string? phone, string? email, string? attendeeName, string? ticketCode, string? cccd = null, string? orderCode = null);
        Task<(List<CheckinLog> Items, int TotalCount, int ApprovedCount, int AlreadyUsedCount, int InvalidCount)> GetScanHistoryAsync(
            int eventId,
            string? gateId,
            string? statusFilter,
            int? staffUserId,
            DateTime? fromDate);
        Task<List<Ticket>> GetOfflinePackageTicketsAsync(int eventId, List<string>? allowedTiers);
        Task AddOfflineConflictAsync(OfflineSyncConflict conflict);
        Task<List<OfflineSyncConflict>> GetOfflineConflictsAsync(int? eventId, string? status);
        Task<OfflineSyncConflict?> GetOfflineConflictByIdAsync(string conflictId);
        Task UpdateOfflineConflictAsync(OfflineSyncConflict conflict);
    }
}
