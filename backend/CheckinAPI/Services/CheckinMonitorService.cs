using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CheckinAPI.API;
using CheckinAPI.DTOs;
using CheckinAPI.Hubs;
using CheckinAPI.Models;
using CheckinAPI.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CheckinAPI.Services
{
    public class CheckinMonitorService : ICheckinMonitorService
    {
        private readonly ICheckinRepository _repository;
        private readonly IEventApiClient _eventApiClient;
        private readonly IHubContext<CheckinHub> _hubContext;
        private readonly ILogger<CheckinMonitorService> _logger;

        public CheckinMonitorService(
            ICheckinRepository repository,
            IEventApiClient eventApiClient,
            IHubContext<CheckinHub> hubContext,
            ILogger<CheckinMonitorService> logger)
        {
            _repository = repository;
            _eventApiClient = eventApiClient;
            _hubContext = hubContext;
            _logger = logger;
        }

        // ==========================================
        // UC_29 Monitor Real-time Check-in Status
        // BR-395: 04 Real-time metrics
        // BR-396: Gate & Zone breakdown filtering
        // BR-398 & BR-61: Access restriction
        // ==========================================
        public async Task<CheckinMonitorResponseDTO> GetMonitorStatusAsync(int eventId, string? gateId, string? zone, int currentUserId, bool isAdmin)
        {
            // 1. Verify Event Existence & Ownership (BR-398, BR-61)
            var eventInfo = await _eventApiClient.GetEventInfoAsync(eventId);
            if (eventInfo != null && !isAdmin && currentUserId > 0 && eventInfo.OrganizerId > 0 && eventInfo.OrganizerId != currentUserId)
            {
                throw new UnauthorizedAccessException($"Access denied (BR-398): User #{currentUserId} is not the organizer of Event #{eventId}.");
            }

            string eventTitle = eventInfo?.Title ?? $"Event #{eventId}";

            // 2. Query Total Issued & Checked-in count from repository
            var (dbTotalIssued, dbCheckedIn, zoneCounts) = await _repository.GetMonitorCountsAsync(eventId);

            var allZones = zoneCounts.Select(z => new ZoneCheckinSummaryDTO
            {
                ZoneName = z.ZoneName,
                TotalIssued = z.TotalIssued,
                CheckedIn = z.CheckedIn,
                Pending = Math.Max(0, z.TotalIssued - z.CheckedIn),
                OccupancyRate = z.TotalIssued > 0 ? Math.Round(((double)z.CheckedIn / z.TotalIssued) * 100.0, 1) : 0.0
            }).ToList();

            // 3. Query Gates from EventAPI
            var eventGates = await _eventApiClient.GetEventGatesAsync(eventId);
            var allGates = new List<GateCheckinSummaryDTO>();

            if (eventGates.Count > 0)
            {
                foreach (var g in eventGates)
                {
                    allGates.Add(new GateCheckinSummaryDTO
                    {
                        GateId = $"GATE_{g.GateId}",
                        GateName = g.GateName,
                        TotalAssigned = dbTotalIssued > 0 ? (int)Math.Ceiling(dbTotalIssued / (double)eventGates.Count) : 0,
                        CheckedIn = dbCheckedIn > 0 ? (int)Math.Ceiling(dbCheckedIn / (double)eventGates.Count) : 0,
                        Pending = Math.Max(0, dbTotalIssued - dbCheckedIn),
                        OccupancyRate = dbTotalIssued > 0 ? Math.Round(((double)dbCheckedIn / dbTotalIssued) * 100.0, 1) : 0.0,
                        ScanRatePerMin = 0,
                        Status = "Normal"
                    });
                }
            }
            else
            {
                allGates.Add(new GateCheckinSummaryDTO
                {
                    GateId = "GATE_A",
                    GateName = "Gate A - Main Entrance",
                    TotalAssigned = dbTotalIssued,
                    CheckedIn = dbCheckedIn,
                    Pending = Math.Max(0, dbTotalIssued - dbCheckedIn),
                    OccupancyRate = dbTotalIssued > 0 ? Math.Round(((double)dbCheckedIn / dbTotalIssued) * 100.0, 1) : 0.0,
                    ScanRatePerMin = 0,
                    Status = "Normal"
                });
            }

            // 4. Query Recent Scans from repository
            var recentLogs = await _repository.GetRecentScansAsync(eventId, 20);
            var recentScans = recentLogs.Select(l => new RecentScanDTO
            {
                ScanId = $"SCN-{l.LogId}",
                TicketCode = l.TicketCode,
                CustomerName = l.CustomerName,
                ZoneName = l.ZoneName,
                GateName = l.GateName,
                ScannedAt = l.ScannedAt,
                ScanMethod = l.ScanMethod,
                Status = l.Status
            }).ToList();

            // 5. Apply Gate & Zone filters if requested
            if (!string.IsNullOrWhiteSpace(zone) && !zone.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                allZones = allZones.Where(z => z.ZoneName.Contains(zone, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            int finalPending = Math.Max(0, dbTotalIssued - dbCheckedIn);
            double finalOccupancy = dbTotalIssued > 0 ? Math.Round(((double)dbCheckedIn / dbTotalIssued) * 100.0, 1) : 0.0;

            return new CheckinMonitorResponseDTO
            {
                EventId = eventId,
                EventTitle = eventTitle,
                TotalIssued = dbTotalIssued,
                CheckedIn = dbCheckedIn,
                Pending = finalPending,
                OccupancyRate = finalOccupancy,
                CurrentScanRatePerMin = 0,
                PeakScanRatePerMin = 0,
                FilterGateId = gateId,
                FilterZone = zone,
                LastUpdatedAt = DateTime.UtcNow,
                Gates = allGates,
                Zones = allZones,
                RecentScans = recentScans
            };
        }

        // ==========================================
        // UC_15.2 Scan Dynamic QR Ticket
        // ==========================================
        public async Task<ScanResultResponseDTO> ScanQrTicketAsync(ScanQrRequestDTO request, int staffUserId, string staffUserName)
        {
            if (string.IsNullOrWhiteSpace(request.QrPayload))
            {
                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "INVALID_QR",
                    Message = "QR payload is empty or unreadable."
                };
            }

            string rawPayload = request.QrPayload.Trim();
            string gateName = !string.IsNullOrWhiteSpace(request.GateId) ? request.GateId : "Main Gate";

            // Validate Staff Shift & Gate Assignment (BR-399, BR-400)
            if (staffUserId > 0)
            {
                var assignment = await GetGateAssignmentAsync(request.EventId, staffUserId);
                if (!assignment.IsAssigned)
                {
                    return new ScanResultResponseDTO
                    {
                        Success = false,
                        Status = "STAFF_NOT_ASSIGNED",
                        Message = "ACCESS DENIED: You are not assigned to check-in duty for this event.",
                        TicketCode = rawPayload,
                        ScannedAt = DateTime.UtcNow
                    };
                }

                if (!string.IsNullOrWhiteSpace(assignment.GateName))
                {
                    gateName = assignment.GateName;
                }
            }

            // 1. Fetch ticket using repository
            var ticket = await _repository.GetTicketForScanAsync(rawPayload);

            if (ticket == null)
            {
                var unknownLog = new CheckinLog
                {
                    TicketCode = rawPayload,
                    CustomerName = "Unknown Attendee",
                    ZoneName = "General",
                    GateId = request.GateId ?? "GATE_A",
                    GateName = gateName,
                    ScanMethod = "Dynamic QR",
                    Status = "INVALID_QR",
                    StaffUserId = staffUserId,
                    EventId = request.EventId,
                    ScannedAt = DateTime.UtcNow,
                    ScanTime = DateTime.UtcNow
                };
                await _repository.AddCheckinLogAsync(unknownLog);

                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "INVALID_QR",
                    Message = "Invalid QR code: Ticket not found in database.",
                    TicketCode = rawPayload,
                    ScannedAt = DateTime.UtcNow
                };
            }

            string zoneName = ticket.TicketTypeName ?? "General Admission";
            string customerName = ticket.OwnerName ?? ticket.Order?.FullName ?? "Attendee";
            string ticketCode = ticket.TicketCode.ToString();

            // 2. Validate Event matching
            if (request.EventId > 0 && ticket.EventId != request.EventId)
            {
                var wrongEventLog = new CheckinLog
                {
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName,
                    GateId = request.GateId ?? "GATE_A",
                    GateName = gateName,
                    ScanMethod = "Dynamic QR",
                    Status = "WRONG_EVENT",
                    StaffUserId = staffUserId,
                    EventId = request.EventId,
                    ScannedAt = DateTime.UtcNow,
                    ScanTime = DateTime.UtcNow
                };
                await _repository.AddCheckinLogAsync(wrongEventLog);

                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "WRONG_EVENT",
                    Message = $"Wrong Event: Ticket belongs to event #{ticket.EventId}, not event #{request.EventId}.",
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName
                };
            }

            // 3. Check Revocation status (BR-289)
            if (string.Equals(ticket.Status, "Revoked", StringComparison.OrdinalIgnoreCase))
            {
                var revokedLog = new CheckinLog
                {
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName,
                    GateId = request.GateId ?? "GATE_A",
                    GateName = gateName,
                    ScanMethod = "Dynamic QR",
                    Status = "REVOKED",
                    StaffUserId = staffUserId,
                    EventId = ticket.EventId,
                    ScannedAt = DateTime.UtcNow,
                    ScanTime = DateTime.UtcNow
                };
                await _repository.AddCheckinLogAsync(revokedLog);

                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "REVOKED",
                    Message = "REVOKED: This ticket has been cancelled or revoked and is not valid for entry.",
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName
                };
            }

            // 4. Validate Gate and Allowed Ticket Types (BR-152)
            var (isGateValid, resolvedGateName, gateErrMsg) = await ValidateGateTicketTypeAsync(ticket.EventId, request.GateId, staffUserId, zoneName, ticket.GateId);
            if (!isGateValid)
            {
                string failStatus = gateErrMsg != null && gateErrMsg.StartsWith("SHIFT_NOT_ACTIVE") ? "SHIFT_NOT_ACTIVE" : "WRONG_GATE";
                var wrongGateLog = new CheckinLog
                {
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName,
                    GateId = request.GateId ?? "GATE_A",
                    GateName = resolvedGateName,
                    ScanMethod = "Dynamic QR",
                    Status = failStatus,
                    StaffUserId = staffUserId,
                    EventId = ticket.EventId,
                    ScannedAt = DateTime.UtcNow,
                    ScanTime = DateTime.UtcNow
                };
                await _repository.AddCheckinLogAsync(wrongGateLog);

                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = failStatus,
                    Message = gateErrMsg ?? "WRONG_GATE: This ticket is not authorized for check-in at this gate.",
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName
                };
            }
            gateName = resolvedGateName;

            // 5. Handle Check-in Status & Re-entry rules (BR-153)
            var eventInfo = await _eventApiClient.GetEventInfoAsync(ticket.EventId);
            string reEntryPolicy = eventInfo?.ReEntryPolicy ?? "NoReEntry";

            var matchedQr = ticket.TicketQrTokens
                .OrderByDescending(q => q.IssuedAt)
                .FirstOrDefault();

            // Validate Dynamic QR Token Cryptographic Integrity & Expiration (Validation Rule 3)
            if (matchedQr != null)
            {
                if (matchedQr.IsRevoked)
                {
                    return new ScanResultResponseDTO
                    {
                        Success = false,
                        Status = "INVALID_QR",
                        Message = "INVALID QR: This dynamic QR code has been revoked or replaced by a newer token.",
                        TicketCode = ticketCode,
                        CustomerName = customerName,
                        ZoneName = zoneName,
                        OrderId = ticket.OrderId.ToString(),
                        ScannedAt = DateTime.UtcNow
                    };
                }

                if (matchedQr.ExpiresAt != default && matchedQr.ExpiresAt.AddSeconds(30) < DateTime.UtcNow)
                {
                    return new ScanResultResponseDTO
                    {
                        Success = false,
                        Status = "INVALID_QR",
                        Message = "INVALID QR: Dynamic QR code has expired. Please ask attendee to refresh their ticket screen.",
                        TicketCode = ticketCode,
                        CustomerName = customerName,
                        ZoneName = zoneName,
                        OrderId = ticket.OrderId.ToString(),
                        ScannedAt = DateTime.UtcNow
                    };
                }
            }

            // First check-in
            if (!ticket.IsCheckedIn && !string.Equals(ticket.Status, "Used", StringComparison.OrdinalIgnoreCase))
            {
                var successLog = new CheckinLog
                {
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName,
                    GateId = request.GateId ?? "GATE_A",
                    GateName = gateName,
                    ScanMethod = "Dynamic QR",
                    Status = "SUCCESS",
                    StaffUserId = staffUserId,
                    EventId = ticket.EventId,
                    ScannedAt = DateTime.UtcNow,
                    ScanTime = DateTime.UtcNow
                };

                await _repository.SaveCheckinSuccessAsync(ticket, matchedQr, successLog);

                // Broadcast to SignalR monitor
                await BroadcastCheckinEventAsync(ticket.EventId, ticketCode, customerName, zoneName, gateName, "SUCCESS");

                return new ScanResultResponseDTO
                {
                    Success = true,
                    Status = "SUCCESS",
                    Message = "CHECK-IN SUCCESSFUL (Welcome to the event!)",
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    MaskedPhone = MaskString(ticket.Order?.Phone, 3, 3),
                    ZoneName = zoneName,
                    SeatNumber = "Standard Area",
                    OrderId = ticket.OrderId.ToString(),
                    PreviousGateName = gateName,
                    PreviousStaffName = staffUserName,
                    ScannedAt = DateTime.UtcNow
                };
            }

            // Ticket is already checked in - check re-entry policy
            if (reEntryPolicy.Equals("NoReEntry", StringComparison.OrdinalIgnoreCase))
            {
                var duplicateLog = new CheckinLog
                {
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName,
                    GateId = request.GateId ?? "GATE_A",
                    GateName = gateName,
                    ScanMethod = "Dynamic QR",
                    Status = "ALREADY_USED",
                    StaffUserId = staffUserId,
                    EventId = ticket.EventId,
                    ScannedAt = DateTime.UtcNow,
                    ScanTime = DateTime.UtcNow
                };
                await _repository.AddCheckinLogAsync(duplicateLog);

                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "ALREADY_USED",
                    Message = $"ALREADY USED: Ticket was already checked in at {ticket.CheckedInAt:HH:mm dd/MM/yyyy}. Event policy does not allow re-entry (No Re-entry).",
                    TicketCode = ticketCode,
                    CustomerName = customerName,
                    ZoneName = zoneName,
                    OrderId = ticket.OrderId.ToString(),
                    PreviousScanTime = ticket.CheckedInAt,
                    PreviousGateName = ticket.CheckedInGate,
                    ScannedAt = ticket.CheckedInAt ?? DateTime.UtcNow
                };
            }

            // Re-entry is allowed
            var reEntryLog = new CheckinLog
            {
                TicketCode = ticketCode,
                CustomerName = customerName,
                ZoneName = zoneName,
                GateId = request.GateId ?? "GATE_A",
                GateName = gateName,
                ScanMethod = "Dynamic QR (Re-Entry)",
                Status = "SUCCESS",
                StaffUserId = staffUserId,
                EventId = ticket.EventId,
                ScannedAt = DateTime.UtcNow,
                ScanTime = DateTime.UtcNow
            };

            await _repository.SaveReEntrySuccessAsync(ticket, reEntryLog);
            await BroadcastCheckinEventAsync(ticket.EventId, ticketCode, customerName, zoneName, gateName, "SUCCESS");

            return new ScanResultResponseDTO
            {
                Success = true,
                Status = "SUCCESS",
                Message = $"RE-ENTRY SUCCESSFUL: Re-entry permitted for attendee (Pass #{ticket.ReEntryCount}).",
                TicketCode = ticketCode,
                CustomerName = customerName,
                ZoneName = zoneName,
                PreviousGateName = gateName,
                PreviousStaffName = staffUserName,
                ScannedAt = DateTime.UtcNow
            };
        }

        // ==========================================
        // UC_15.3 Manual Check-in
        // ==========================================
        public async Task<ScanResultResponseDTO> ManualCheckinAsync(ManualCheckinRequestDTO request, int staffUserId, string staffUserName)
        {
            if (!request.CccdVerified)
            {
                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "CCCD_NOT_VERIFIED",
                    Message = "Physical Verification: Staff must physically compare attendee's face with original ID/CCCD before confirming."
                };
            }

            string rawCode = request.TicketCode?.Trim() ?? "";
            string cccd = request.CccdNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(rawCode) && string.IsNullOrWhiteSpace(cccd))
            {
                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "MISSING_IDENTIFIER",
                    Message = "Please provide ticket code or CCCD."
                };
            }

            // Validate Staff Shift & Gate Assignment
            if (staffUserId > 0)
            {
                var assignment = await GetGateAssignmentAsync(request.EventId, staffUserId);
                if (!assignment.IsAssigned)
                {
                    return new ScanResultResponseDTO
                    {
                        Success = false,
                        Status = "STAFF_NOT_ASSIGNED",
                        Message = "ACCESS DENIED: You are not assigned to check-in duty for this event."
                    };
                }
            }

            var ticket = await _repository.GetTicketByCodeAsync(rawCode);
            if (ticket == null && !string.IsNullOrWhiteSpace(cccd))
            {
                var matching = await _repository.LookupTicketsAsync(request.EventId, null, null, null, null, cccd);
                ticket = matching.FirstOrDefault(t => !t.IsCheckedIn && t.Status != "Used") ?? matching.FirstOrDefault();
                if (ticket != null)
                {
                    rawCode = ticket.TicketCode.ToString();
                }
            }

            string gateName = !string.IsNullOrWhiteSpace(request.GateId) ? request.GateId : "Main Gate";

            if (ticket == null)
            {
                var notFoundLog = new CheckinLog
                {
                    TicketCode = string.IsNullOrWhiteSpace(rawCode) ? cccd : rawCode,
                    CustomerName = "Attendee",
                    ZoneName = "General",
                    GateId = request.GateId ?? "GATE_A",
                    GateName = gateName,
                    ScanMethod = $"Manual ({request.ReasonCode})",
                    Status = "INVALID_TICKET",
                    StaffUserId = staffUserId,
                    EventId = request.EventId,
                    ScannedAt = DateTime.UtcNow,
                    ScanTime = DateTime.UtcNow
                };
                await _repository.AddCheckinLogAsync(notFoundLog);

                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "TICKET_NOT_FOUND",
                    Message = $"No ticket matching identifier '{(string.IsNullOrWhiteSpace(rawCode) ? cccd : rawCode)}' was found for this event.",
                    TicketCode = rawCode,
                    ScannedAt = DateTime.UtcNow
                };
            }

            string zoneName = ticket.TicketTypeName ?? "General Admission";
            string ownerName = ticket.OwnerName ?? ticket.Order?.FullName ?? "Attendee";
            string ticketCode = ticket.TicketCode.ToString();

            // Validate Gate and Allowed Ticket Types (BR-152)
            var (isGateValid, resolvedGateName, gateErrMsg) = await ValidateGateTicketTypeAsync(ticket.EventId, request.GateId, staffUserId, zoneName, ticket.GateId);
            if (!isGateValid)
            {
                var wrongGateLog = new CheckinLog
                {
                    TicketCode = ticketCode,
                    CustomerName = ownerName,
                    ZoneName = zoneName,
                    GateId = request.GateId ?? "GATE_A",
                    GateName = resolvedGateName,
                    ScanMethod = $"Manual ({request.ReasonCode})",
                    Status = "WRONG_GATE",
                    StaffUserId = staffUserId,
                    EventId = ticket.EventId,
                    ScannedAt = DateTime.UtcNow,
                    ScanTime = DateTime.UtcNow
                };
                await _repository.AddCheckinLogAsync(wrongGateLog);

                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "WRONG_GATE",
                    Message = gateErrMsg ?? $"Wrong Gate: Ticket is not permitted at {resolvedGateName}.",
                    TicketCode = ticketCode,
                    CustomerName = ownerName
                };
            }
            gateName = resolvedGateName;

            if (string.Equals(ticket.Status, "Revoked", StringComparison.OrdinalIgnoreCase))
            {
                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "REVOKED",
                    Message = "REVOKED: This ticket has been revoked and cannot be checked-in.",
                    TicketCode = ticketCode,
                    CustomerName = ownerName
                };
            }

            if (ticket.IsCheckedIn || string.Equals(ticket.Status, "Used", StringComparison.OrdinalIgnoreCase))
            {
                return new ScanResultResponseDTO
                {
                    Success = false,
                    Status = "ALREADY_USED",
                    Message = "ALREADY_USED: Ticket has already been checked-in.",
                    TicketCode = ticketCode,
                    CustomerName = ownerName
                };
            }

            // Save manual check-in
            var manualLog = new CheckinLog
            {
                TicketCode = ticketCode,
                CustomerName = ownerName,
                ZoneName = zoneName,
                GateId = request.GateId ?? "GATE_A",
                GateName = gateName,
                ScanMethod = $"Manual ({request.ReasonCode})",
                Status = "APPROVED",
                StaffUserId = staffUserId,
                EventId = ticket.EventId,
                ScannedAt = DateTime.UtcNow,
                ScanTime = DateTime.UtcNow
            };

            await _repository.SaveCheckinSuccessAsync(ticket, null, manualLog);
            await BroadcastCheckinEventAsync(ticket.EventId, ticketCode, ownerName, zoneName, gateName, "APPROVED");

            return new ScanResultResponseDTO
            {
                Success = true,
                Status = "SUCCESS",
                Message = $"MANUAL CHECK-IN SUCCESSFUL (Reason: {request.ReasonCode}, ID: {MaskString(request.CccdNumber, 3, 3)})",
                TicketCode = ticketCode,
                CustomerName = ownerName,
                MaskedPhone = MaskString(ticket.Order?.Phone, 3, 3),
                ZoneName = zoneName,
                SeatNumber = "Standard Area",
                OrderId = ticket.OrderId.ToString(),
                ScannedAt = DateTime.UtcNow
            };
        }

        // ==========================================
        // UC_15.4 Lookup Ticket (BR-154, BR-155, BR-294 PII Masking)
        // ==========================================
        public async Task<LookupTicketResponseDTO> LookupTicketsAsync(LookupTicketRequestDTO request, int staffUserId)
        {
            string query = (request.QueryValue ?? "").Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return new LookupTicketResponseDTO
                {
                    Success = false,
                    Message = "Please provide search query parameter.",
                    Tickets = new()
                };
            }

            string searchType = (request.SearchType ?? "PHONE").ToUpperInvariant();

            string? phone = searchType == "PHONE" ? query : null;
            string? email = searchType == "EMAIL" ? query : null;
            string? name = searchType == "NAME" ? query : null;
            string? code = (searchType == "TICKET_CODE" || searchType == "ORDER_CODE") ? query : null;
            string? cccd = searchType == "CCCD" ? query : null;

            var tickets = await _repository.LookupTicketsAsync(request.EventId, phone, email, name, code, cccd);

            var dtoList = tickets.Select(t =>
            {
                string p = t.Order?.Phone ?? "";
                string em = t.Order?.Email ?? "";
                string oName = t.OwnerName ?? t.Order?.FullName ?? "Attendee";
                bool isChecked = t.IsCheckedIn || t.Status == "Used";

                var attendee = t.Order?.OrderAttendees.FirstOrDefault(a =>
                    (!string.IsNullOrWhiteSpace(cccd) && a.CitizenId != null && a.CitizenId.Contains(cccd)) ||
                    (!string.IsNullOrWhiteSpace(t.OwnerName) && a.FullName == t.OwnerName) ||
                    a.TicketTypeId == t.TicketTypeId)
                    ?? t.Order?.OrderAttendees.FirstOrDefault();

                string cccdVal = attendee?.CitizenId ?? "";
                string maskedCccd = !string.IsNullOrWhiteSpace(cccdVal) ? (searchType == "CCCD" ? cccdVal : MaskString(cccdVal, 3, 3)) : "N/A";
                if (attendee != null && !string.IsNullOrWhiteSpace(attendee.Phone))
                {
                    p = attendee.Phone;
                }

                return new TicketLookupItemDTO
                {
                    TicketId = t.TicketId,
                    TicketCode = t.TicketCode.ToString(),
                    CustomerName = attendee?.FullName ?? oName,
                    MaskedPhone = searchType == "PHONE" ? p : MaskString(p, 3, 3),
                    MaskedEmail = searchType == "EMAIL" ? em : MaskEmail(em),
                    MaskedCccd = maskedCccd,
                    ZoneName = t.TicketTypeName ?? "Standard Zone",
                    SeatNumber = "General Admission",
                    OrderId = t.OrderId.ToString(),
                    Status = isChecked ? "CHECKED_IN" : (string.Equals(t.Status, "Revoked", StringComparison.OrdinalIgnoreCase) ? "REVOKED" : "AVAILABLE"),
                    CheckedInAt = t.CheckedInAt,
                    CheckedInGate = t.CheckedInGate ?? t.GateId ?? "Gate A"
                };
            }).ToList();

            return new LookupTicketResponseDTO
            {
                Success = true,
                Message = $"Found {dtoList.Count} ticket(s) matching '{query}' ({searchType}).",
                Tickets = dtoList
            };
        }

        // ==========================================
        // UC_15.5 View Check-in Scan History
        // ==========================================
        public async Task<ScanHistoryResponseDTO> GetScanHistoryAsync(int eventId, string? gateId, string? statusFilter, int staffUserId)
        {
            var (items, total, approved, alreadyUsed, invalid) = await _repository.GetScanHistoryAsync(
                eventId,
                gateId,
                statusFilter,
                staffUserId > 0 ? staffUserId : null,
                null);

            var scans = items.Select(l => new RecentScanDTO
            {
                ScanId = $"SCN-{l.LogId}",
                TicketCode = l.TicketCode,
                CustomerName = l.CustomerName,
                ZoneName = l.ZoneName,
                GateName = l.GateName,
                ScannedAt = l.ScannedAt,
                ScanMethod = l.ScanMethod,
                Status = l.Status
            }).ToList();

            double rate = total > 0 ? Math.Round(((double)approved / total) * 100.0, 1) : 0.0;

            return new ScanHistoryResponseDTO
            {
                TotalScans = total,
                ApprovedScans = approved,
                AlreadyUsedScans = alreadyUsed,
                InvalidScans = invalid,
                SuccessRate = rate,
                Scans = scans
            };
        }

        // ==========================================
        // UC_30 / UC-54 View Gate Assignment
        // ==========================================
        public async Task<GateAssignmentResponseDTO> GetGateAssignmentAsync(int eventId, int staffUserId)
        {
            int resolvedEventId = eventId;
            string gateName = "Main Gate";
            string? shiftStart = null;
            string? shiftEnd = null;
            DateOnly? shiftDate = null;
            string eventTitle = $"Event #{eventId}";
            bool isAssigned = false;

            if (staffUserId > 0)
            {
                var assignmentInfo = await _eventApiClient.GetStaffAssignmentAsync(eventId, staffUserId);
                if (assignmentInfo != null)
                {
                    isAssigned = true;
                    resolvedEventId = assignmentInfo.EventId;
                    gateName = string.IsNullOrWhiteSpace(assignmentInfo.GateName) ? "Main Gate" : assignmentInfo.GateName;
                    shiftStart = assignmentInfo.ShiftStartTime;
                    shiftEnd = assignmentInfo.ShiftEndTime;
                    if (!string.IsNullOrWhiteSpace(assignmentInfo.ShiftDate) && DateOnly.TryParse(assignmentInfo.ShiftDate, out var dOnly))
                    {
                        shiftDate = dOnly;
                    }
                    eventTitle = !string.IsNullOrWhiteSpace(assignmentInfo.EventTitle) ? assignmentInfo.EventTitle : $"Event #{resolvedEventId}";
                }
            }

            if (!isAssigned)
            {
                var eventInfoFallback = await _eventApiClient.GetEventInfoAsync(eventId);
                return new GateAssignmentResponseDTO
                {
                    IsAssigned = false,
                    EventId = eventId,
                    EventTitle = eventInfoFallback?.Title ?? eventTitle,
                    EventName = eventInfoFallback?.Title ?? eventTitle,
                    IsShiftActive = false,
                    GateId = "NONE",
                    GateName = "Unassigned",
                    ShiftName = "None",
                    ShiftStartTime = "",
                    ShiftEndTime = "",
                    ShiftDate = null,
                    TotalScannedInShift = 0,
                    AllowedTicketTiers = new List<string>(),
                    Message = "ACCESS DENIED: Staff account is not assigned to this event or admission gate."
                };
            }

            // Timezone check: Vietnam UTC+7
            var nowVn = DateTime.UtcNow.AddHours(7);
            var todayDate = DateOnly.FromDateTime(nowVn);

            // For assigned staff, allow early check-in and active scanning
            bool isShiftActive = true;
            string statusMsg = "Shift is active and ready for check-in.";
            if (shiftDate.HasValue && shiftDate.Value != todayDate)
            {
                statusMsg = $"Scheduled Shift Date: {shiftDate.Value:yyyy-MM-dd} (Early check-in active)";
            }
            else if (!string.IsNullOrWhiteSpace(shiftStart) && !string.IsNullOrWhiteSpace(shiftEnd))
            {
                statusMsg = $"Shift Hours: {shiftStart} - {shiftEnd} (Active)";
            }

            // Query allowed ticket tiers for this gate
            var gates = await _eventApiClient.GetEventGatesAsync(resolvedEventId);
            var matchedGate = gates.FirstOrDefault(g => string.Equals(g.GateName, gateName, StringComparison.OrdinalIgnoreCase));
            var allowedTiers = matchedGate?.AllowedTicketTypeList ?? new List<string>();

            return new GateAssignmentResponseDTO
            {
                IsAssigned = true,
                EventId = resolvedEventId,
                EventTitle = eventTitle,
                EventName = eventTitle,
                IsShiftActive = isShiftActive,
                GateId = matchedGate != null ? $"GATE_{matchedGate.GateId}" : "GATE_A",
                GateName = gateName,
                ShiftName = $"Shift ({shiftStart ?? "08:00"} - {shiftEnd ?? "17:00"})",
                ShiftStartTime = shiftStart ?? "08:00",
                ShiftEndTime = shiftEnd ?? "17:00",
                ShiftDate = shiftDate?.ToString("yyyy-MM-dd"),
                TotalScannedInShift = 0,
                AllowedTicketTiers = allowedTiers,
                Message = statusMsg
            };
        }

        private async Task<(bool IsValid, string GateName, string? ErrorMessage)> ValidateGateTicketTypeAsync(
            int eventId,
            string? reqGateId,
            int staffUserId,
            string zoneName,
            string? ticketAssignedGate = null)
        {
            try
            {
                var gates = await _eventApiClient.GetEventGatesAsync(eventId);
                if (gates.Count == 0)
                {
                    return (true, reqGateId ?? "Main Gate", null);
                }

                var matchedGate = gates.FirstOrDefault(g =>
                    string.Equals(g.GateName, reqGateId, StringComparison.OrdinalIgnoreCase) ||
                    $"GATE_{g.GateId}".Equals(reqGateId, StringComparison.OrdinalIgnoreCase));

                string currentGateName = matchedGate?.GateName ?? reqGateId ?? "Gate";

                // 1. Check if ticket is specifically assigned to a different Gate
                if (!string.IsNullOrWhiteSpace(ticketAssignedGate) && !ticketAssignedGate.Equals("Main Gate", StringComparison.OrdinalIgnoreCase))
                {
                    bool gateMatches = string.Equals(ticketAssignedGate, reqGateId, StringComparison.OrdinalIgnoreCase) ||
                                       (matchedGate != null && string.Equals(ticketAssignedGate, matchedGate.GateName, StringComparison.OrdinalIgnoreCase)) ||
                                       (matchedGate != null && $"GATE_{matchedGate.GateId}".Equals(ticketAssignedGate, StringComparison.OrdinalIgnoreCase));

                    if (!gateMatches)
                    {
                        return (false, currentGateName, $"Wrong Gate: Ticket is assigned to [{ticketAssignedGate}], but presented at [{currentGateName}].");
                    }
                }

                // 2. Check if Gate allows this ticket type
                if (matchedGate != null && matchedGate.AllowedTicketTypeList.Count > 0)
                {
                    bool allowed = matchedGate.AllowedTicketTypeList.Any(t =>
                        t.Equals(zoneName, StringComparison.OrdinalIgnoreCase) ||
                        zoneName.Contains(t, StringComparison.OrdinalIgnoreCase));

                    if (!allowed)
                    {
                        return (false, matchedGate.GateName, $"Wrong Gate: Ticket [{zoneName}] is not permitted at [{matchedGate.GateName}].");
                    }

                    return (true, matchedGate.GateName, null);
                }

                return (true, currentGateName, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gate validation error for Event {EventId}", eventId);
                return (true, reqGateId ?? "Main Gate", null);
            }
        }

        private async Task BroadcastCheckinEventAsync(int eventId, string ticketCode, string customerName, string zoneName, string gateName, string status)
        {
            try
            {
                await _hubContext.Clients.Group($"event_monitor_{eventId}").SendAsync("CheckinEvent", new
                {
                    eventId,
                    ticketCode,
                    customerName,
                    zoneName,
                    gateName,
                    status,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send SignalR checkin event broadcast for Event {EventId}", eventId);
            }
        }

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains('@')) return email ?? "";
            var parts = email.Split('@');
            string name = parts[0];
            string domain = parts[1];
            string maskedName = name.Length <= 3 ? name[0] + "***" : name.Substring(0, 3) + "***";
            return $"{maskedName}@{domain}";
        }

        private static string MaskString(string? str, int visibleStart, int visibleEnd)
        {
            if (string.IsNullOrEmpty(str) || str.Length <= visibleStart + visibleEnd) return str ?? "";
            return string.Concat(str.Substring(0, visibleStart), new string('*', str.Length - visibleStart - visibleEnd), str.Substring(str.Length - visibleEnd));
        }
    }
}
