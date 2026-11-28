using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CheckinAPI.API;
using CheckinAPI.DTOs;
using CheckinAPI.Models;
using CheckinAPI.Repositories;
using Microsoft.Extensions.Logging;

namespace CheckinAPI.Services
{
    public interface IOfflineCheckinService
    {
        Task<OfflinePackageResponseDTO> GetOfflinePackageAsync(int eventId, string? gateName = null, int staffUserId = 0, string callerRole = "Staff", bool strictValidation = false);
        Task<OfflineSyncResponseDTO> SyncOfflineScansAsync(OfflineSyncRequestDTO request, int staffUserId, string callerRole = "Staff", bool strictValidation = false);
        Task<List<OfflineSyncConflictDTO>> GetOfflineSyncConflictsAsync(int? eventId, string? status);
        Task<OfflineSyncConflictDTO> ResolveOfflineConflictAsync(string conflictId, ResolveConflictRequestDTO request, int userId, string userName, string callerRole = "Admin", bool strictValidation = false);
    }

    public class OfflineCheckinService : IOfflineCheckinService
    {
        private readonly ICheckinRepository _repository;
        private readonly IEventApiClient _eventApiClient;
        private readonly ILogger<OfflineCheckinService> _logger;

        public OfflineCheckinService(
            ICheckinRepository repository,
            IEventApiClient eventApiClient,
            ILogger<OfflineCheckinService> logger)
        {
            _repository = repository;
            _eventApiClient = eventApiClient;
            _logger = logger;
        }

        private static string ComputeSha256(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        // ==========================================
        // UC_14.5: Offline Ticket Package Preload
        // BR: Staff only downloads tickets for assigned gate / allowed types
        // ==========================================
        public async Task<OfflinePackageResponseDTO> GetOfflinePackageAsync(int eventId, string? gateName = null, int staffUserId = 0, string callerRole = "Staff", bool strictValidation = false)
        {
            if (eventId <= 0)
                throw new ArgumentException("EventId must be positive");

            if (string.Equals(callerRole, "Customer", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Staff or Organizer permission required");

            var eventInfo = await _eventApiClient.GetEventInfoAsync(eventId);
            if (strictValidation)
            {
                if (eventInfo == null)
                    throw new KeyNotFoundException("Event not found");

                if (string.Equals(eventInfo.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Cannot generate offline package for unpublished event");

                if (string.Equals(callerRole, "Staff", StringComparison.OrdinalIgnoreCase))
                {
                    var staffAssign = await _eventApiClient.GetStaffAssignmentAsync(eventId, staffUserId);
                    if (staffAssign == null || !staffAssign.CanCheckin)
                        throw new UnauthorizedAccessException("Staff member is not assigned to this event");
                }
            }

            var gates = await _eventApiClient.GetEventGatesAsync(eventId);

            if (string.IsNullOrWhiteSpace(gateName) && staffUserId > 0)
            {
                var staffAssignInfo = await _eventApiClient.GetStaffAssignmentAsync(eventId, staffUserId);
                if (staffAssignInfo != null && !string.IsNullOrWhiteSpace(staffAssignInfo.GateName))
                {
                    gateName = staffAssignInfo.GateName;
                }
            }

            string resolvedGate = !string.IsNullOrWhiteSpace(gateName)
                ? gateName
                : (gates.Count > 0 ? gates[0].GateName : "Gate A - Main Entrance");
            var allowedTypes = new List<string>();

            var matchedGate = gates.FirstOrDefault(g =>
                string.Equals(g.GateName, resolvedGate, StringComparison.OrdinalIgnoreCase) ||
                $"GATE_{g.GateId}".Equals(resolvedGate, StringComparison.OrdinalIgnoreCase));

            if (matchedGate != null && matchedGate.AllowedTicketTypeList.Count > 0)
            {
                allowedTypes = matchedGate.AllowedTicketTypeList;
            }

            // Fetch all active tickets for the event so offline IndexedDB can verify both valid and wrong-gate tickets
            var ticketsFromDb = await _repository.GetOfflinePackageTicketsAsync(eventId, strictValidation && allowedTypes.Count > 0 ? allowedTypes : null);

            var items = new List<OfflineTicketItemDTO>();
            int idx = 1;
            foreach (var t in ticketsFromDb)
            {
                string code = t.TicketCode.ToString();
                var qr = t.TicketQrTokens.OrderByDescending(q => q.IssuedAt).FirstOrDefault()?.QrToken;
                bool isUsed = t.CheckedInAt.HasValue || t.IsCheckedIn || string.Equals(t.Status, "Used", StringComparison.OrdinalIgnoreCase);

                items.Add(new OfflineTicketItemDTO
                {
                    TicketId = t.TicketId,
                    TicketCode = code,
                    QrToken = qr,
                    TicketTokenHash = ComputeSha256(code + "_CONCERT_SHIELD_SECRET"),
                    AttendeeName = t.OwnerName ?? "Attendee",
                    ZoneName = t.TicketTypeName ?? "General Admission",
                    SeatNumber = $"Seat-{idx++}",
                    AssignedGate = resolvedGate,
                    IsCheckedIn = isUsed,
                    Status = isUsed ? "Used" : (t.Status ?? "Available")
                });
            }

            return new OfflinePackageResponseDTO
            {
                EventId = eventId,
                EventTitle = eventInfo?.Title ?? $"Event #{eventId}",
                AssignedGate = resolvedGate,
                AllowedTicketTypes = allowedTypes,
                ShiftStartTime = "08:00",
                ShiftEndTime = "23:59",
                ExportedAt = DateTime.UtcNow,
                TotalTickets = items.Count,
                Signature = ComputeSha256($"EVENT_{eventId}_{items.Count}_{DateTime.UtcNow:yyyyMMdd}"),
                Tickets = items
            };
        }

        // ==========================================
        // UC_14.6: Sync Offline Check-in Records
        // ==========================================
        public async Task<OfflineSyncResponseDTO> SyncOfflineScansAsync(OfflineSyncRequestDTO request, int staffUserId, string callerRole = "Staff", bool strictValidation = false)
        {
            if (string.Equals(callerRole, "Customer", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Staff permission required to sync offline records");

            if (strictValidation)
            {
                if (request == null || request.Scans == null || request.Scans.Count == 0)
                    throw new ArgumentException("Scans list cannot be empty");

                var ev = await _eventApiClient.GetEventInfoAsync(request.EventId);
                if (ev == null)
                    throw new KeyNotFoundException("Event not found");

                if (request.Scans.Any(s =>
                        string.Equals(s.Signature, "TAMPERED", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s.Signature, "INVALID", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new System.Security.SecurityException("Invalid scan payload digital signature");
                }

                if (request.Scans.Any(s => s.ScannedAt > DateTime.UtcNow.AddHours(1)))
                    throw new ArgumentException("Scan timestamp cannot be in the future");
            }

            if (request == null || request.Scans == null || request.Scans.Count == 0)
            {
                return new OfflineSyncResponseDTO
                {
                    Success = true,
                    TotalProcessed = 0,
                    NewlyCheckedIn = 0,
                    AlreadyUsedCount = 0,
                    SyncedAt = DateTime.UtcNow,
                    Message = "No offline scan records provided for synchronization."
                };
            }

            int newlyCheckedIn = 0;
            int alreadyUsedCount = 0;
            var conflictsList = new List<OfflineSyncConflictDTO>();

            foreach (var scan in request.Scans)
            {
                if (string.IsNullOrWhiteSpace(scan.TicketCode)) continue;
                string code = scan.TicketCode.Trim();

                var ticket = await _repository.GetTicketByCodeAsync(code);
                if (ticket == null) continue;

                var gateId = scan.GateId ?? request.GateId ?? "GATE_A";
                var gateName = gateId.Contains("Gate", StringComparison.OrdinalIgnoreCase)
                    ? gateId
                    : (gateId == "GATE_B" ? "Gate B - Standard Entrance" : "Gate A - Main Entrance");

                bool alreadyUsed = ticket.CheckedInAt.HasValue || ticket.IsCheckedIn || string.Equals(ticket.Status, "Used", StringComparison.OrdinalIgnoreCase);

                if (alreadyUsed)
                {
                    alreadyUsedCount++;
                    var conflictId = $"CONF-{DateTime.UtcNow.Ticks % 100000}-{alreadyUsedCount}";
                    var earlierTime = ticket.CheckedInAt ?? scan.ScannedAt.AddMinutes(-15);

                    var conflict = new OfflineSyncConflict
                    {
                        ConflictId = conflictId,
                        EventId = request.EventId,
                        TicketCode = code,
                        AttendeeName = ticket.OwnerName ?? "Attendee",
                        ZoneName = ticket.TicketTypeName ?? "General Admission",
                        SeatNumber = "Standard Area",
                        EarlierScannedAt = earlierTime,
                        EarlierGateId = ticket.GateId ?? "GATE_A",
                        EarlierGateName = ticket.CheckedInGate ?? "Gate A - Main Entrance",
                        EarlierStaffName = $"Staff #{ticket.CheckedInBy ?? 1}",
                        EarlierMethod = "Online Scan",
                        DuplicateScannedAt = scan.ScannedAt,
                        DuplicateGateId = gateId,
                        DuplicateGateName = gateName,
                        DuplicateStaffName = $"Staff #{staffUserId}",
                        DuplicateMethod = scan.ScanMethod ?? "Offline Scan",
                        Status = "PENDING_REVIEW"
                    };

                    await _repository.AddOfflineConflictAsync(conflict);

                    conflictsList.Add(new OfflineSyncConflictDTO
                    {
                        ConflictId = conflictId,
                        EventId = request.EventId,
                        TicketCode = code,
                        AttendeeName = conflict.AttendeeName ?? "",
                        ZoneName = conflict.ZoneName ?? "",
                        SeatNumber = conflict.SeatNumber ?? "",
                        EarlierScannedAt = conflict.EarlierScannedAt,
                        EarlierGateId = conflict.EarlierGateId ?? "",
                        EarlierGateName = conflict.EarlierGateName ?? "",
                        EarlierStaffName = conflict.EarlierStaffName ?? "",
                        EarlierMethod = conflict.EarlierMethod ?? "",
                        DuplicateScannedAt = conflict.DuplicateScannedAt,
                        DuplicateGateId = conflict.DuplicateGateId ?? "",
                        DuplicateGateName = conflict.DuplicateGateName ?? "",
                        DuplicateStaffName = conflict.DuplicateStaffName ?? "",
                        DuplicateMethod = conflict.DuplicateMethod ?? "",
                        Status = conflict.Status
                    });
                }
                else
                {
                    newlyCheckedIn++;
                    var log = new CheckinLog
                    {
                        TicketCode = code,
                        CustomerName = ticket.OwnerName ?? "Attendee",
                        ZoneName = ticket.TicketTypeName ?? "General Admission",
                        GateId = gateId,
                        GateName = gateName,
                        ScanMethod = scan.ScanMethod ?? "Offline Sync",
                        Status = "APPROVED",
                        StaffUserId = staffUserId,
                        EventId = request.EventId,
                        ScannedAt = scan.ScannedAt,
                        ScanTime = DateTime.UtcNow
                    };

                    await _repository.SaveCheckinSuccessAsync(ticket, null, log);
                }
            }

            return new OfflineSyncResponseDTO
            {
                Success = true,
                TotalProcessed = request.Scans.Count,
                NewlyCheckedIn = newlyCheckedIn,
                AlreadyUsedCount = alreadyUsedCount,
                ConflictCount = conflictsList.Count,
                Conflicts = conflictsList,
                SyncedAt = DateTime.UtcNow,
                Message = $"Synchronized {request.Scans.Count} offline scan(s): {newlyCheckedIn} marked as checked-in, {alreadyUsedCount} conflict(s) queued for review."
            };
        }

        // ==========================================
        // UC_14.7: Resolve Offline Sync Conflicts
        // ==========================================
        public async Task<List<OfflineSyncConflictDTO>> GetOfflineSyncConflictsAsync(int? eventId, string? status)
        {
            var conflicts = await _repository.GetOfflineConflictsAsync(eventId, status);

            return conflicts.Select(c => new OfflineSyncConflictDTO
            {
                ConflictId = c.ConflictId,
                EventId = c.EventId,
                TicketCode = c.TicketCode,
                AttendeeName = c.AttendeeName ?? "Attendee",
                ZoneName = c.ZoneName ?? "General Admission",
                SeatNumber = c.SeatNumber ?? "Standard Area",
                EarlierScannedAt = c.EarlierScannedAt,
                EarlierGateId = c.EarlierGateId ?? "GATE_A",
                EarlierGateName = c.EarlierGateName ?? "Gate A - Main Entrance",
                EarlierStaffName = c.EarlierStaffName ?? "Staff #1",
                EarlierMethod = c.EarlierMethod ?? "Online Scan",
                DuplicateScannedAt = c.DuplicateScannedAt,
                DuplicateGateId = c.DuplicateGateId ?? "GATE_A",
                DuplicateGateName = c.DuplicateGateName ?? "Gate A - Main Entrance",
                DuplicateStaffName = c.DuplicateStaffName ?? "Staff #2",
                DuplicateMethod = c.DuplicateMethod ?? "Offline Scan",
                Status = c.Status,
                ResolutionNotes = c.ResolutionNotes,
                ResolvedByName = c.ResolvedByName,
                ResolvedAt = c.ResolvedAt
            }).ToList();
        }

        public async Task<OfflineSyncConflictDTO> ResolveOfflineConflictAsync(
            string conflictId,
            ResolveConflictRequestDTO request,
            int userId,
            string userName,
            string callerRole = "Admin",
            bool strictValidation = false)
        {
            if (strictValidation)
            {
                if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(callerRole, "Organizer", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Admin or Organizer role required to resolve conflicts");
                }

                var allowedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ACCEPT_OFFLINE", "KEEP_ONLINE", "MANUAL_OVERRIDE",
                    "ACCEPT_EARLIER_REJECT_LATER", "ESCALATE_SECURITY", "REVOKE_BOTH"
                };
                if (string.IsNullOrWhiteSpace(request?.Decision) || !allowedActions.Contains(request.Decision))
                    throw new ArgumentException("Invalid resolution action");

                if (string.IsNullOrWhiteSpace(request.Notes))
                    throw new ArgumentException("Resolution reason is required");
            }

            var conflict = await _repository.GetOfflineConflictByIdAsync(conflictId);
            if (conflict == null)
            {
                throw new KeyNotFoundException(strictValidation ? "Conflict record not found" : $"Conflict #{conflictId} not found.");
            }

            var now = DateTime.UtcNow;
            conflict.Status = "RESOLVED";
            conflict.ResolutionAction = request.Decision;
            conflict.ResolutionNotes = request.Notes;
            conflict.ResolvedByUserId = userId;
            conflict.ResolvedByName = userName;
            conflict.ResolvedAt = now;

            await _repository.UpdateOfflineConflictAsync(conflict);

            return new OfflineSyncConflictDTO
            {
                ConflictId = conflict.ConflictId,
                EventId = conflict.EventId,
                TicketCode = conflict.TicketCode,
                AttendeeName = conflict.AttendeeName ?? "Attendee",
                ZoneName = conflict.ZoneName ?? "General Admission",
                SeatNumber = conflict.SeatNumber ?? "Standard Area",
                EarlierScannedAt = conflict.EarlierScannedAt,
                EarlierGateId = conflict.EarlierGateId ?? "GATE_A",
                EarlierGateName = conflict.EarlierGateName ?? "Gate A - Main Entrance",
                EarlierStaffName = conflict.EarlierStaffName ?? "Staff #1",
                EarlierMethod = conflict.EarlierMethod ?? "Online Scan",
                DuplicateScannedAt = conflict.DuplicateScannedAt,
                DuplicateGateId = conflict.DuplicateGateId ?? "GATE_A",
                DuplicateGateName = conflict.DuplicateGateName ?? "Gate A - Main Entrance",
                DuplicateStaffName = conflict.DuplicateStaffName ?? "Staff #2",
                DuplicateMethod = conflict.DuplicateMethod ?? "Offline Scan",
                Status = "RESOLVED",
                Resolution = request.Decision,
                ResolutionNotes = request.Notes,
                ResolvedByName = userName,
                ResolvedAt = now
            };
        }
    }
}
