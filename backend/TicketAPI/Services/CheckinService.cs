using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;

namespace TicketAPI.Services
{
    public class CheckinService : ICheckinService
    {
        private readonly ICheckinRepository _checkinRepository;
        private readonly IEventApiClient _eventApiClient;
        private readonly ILogger<CheckinService> _logger;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public CheckinService(
            ICheckinRepository checkinRepository,
            IEventApiClient eventApiClient,
            ILogger<CheckinService> logger)
        {
            _checkinRepository = checkinRepository;
            _eventApiClient = eventApiClient;
            _logger = logger;
        }

        public CheckinService(
            TicketDbContext context,
            IEventApiClient eventApiClient,
            ILogger<CheckinService> logger)
            : this(new CheckinRepository(context), eventApiClient, logger)
        {
        }

        public async Task<CheckinScanResponseDto> ProcessScanAsync(CheckinScanRequestDto request, int staffUserId)
        {
            if (request == null)
            {
                return new CheckinScanResponseDto
                {
                    Success = false,
                    ResultCode = "Invalid",
                    Message = "Dữ liệu quét không hợp lệ."
                };
            }

            Ticket? ticket = null;

            if (request.TicketCode.HasValue)
            {
                ticket = await _checkinRepository.GetTicketByCodeAsync(request.TicketCode.Value);
            }
            else if (!string.IsNullOrWhiteSpace(request.QrToken))
            {
                if (Guid.TryParse(request.QrToken, out var parsedCode))
                {
                    ticket = await _checkinRepository.GetTicketByCodeAsync(parsedCode);
                }

                if (ticket == null)
                {
                    ticket = await _checkinRepository.GetTicketByQrTokenAsync(request.QrToken);
                }
            }
            else if (request.TicketId.HasValue)
            {
                ticket = await _checkinRepository.GetTicketByIdAsync(request.TicketId.Value);
            }

            if (ticket == null)
            {
                return new CheckinScanResponseDto
                {
                    Success = false,
                    ResultCode = "Invalid",
                    Message = "Ticket not found in system."
                };
            }

            var isValidTicketStatus = ticket.Status == "Active" || (ticket.Status == "Used" && ticket.CheckedInAt.HasValue);
            if (!isValidTicketStatus || (ticket.Order != null && ticket.Order.Status != "Paid"))
            {
                return new CheckinScanResponseDto
                {
                    Success = false,
                    ResultCode = "InvalidStatus",
                    TicketId = ticket.TicketId,
                    TicketTypeName = ticket.TicketTypeName,
                    OwnerName = ticket.OwnerName,
                    SeatId = ticket.SeatId,
                    Message = $"Ticket is not valid for entry (Status: {ticket.Status})."
                };
            }

            var eventInfo = await _eventApiClient.GetEventByIdAsync(ticket.EventId);
            var reEntryPolicy = eventInfo?.ReEntryPolicy ?? "NoReEntry";
            var isExit = request.ScanType.Equals("Exit", StringComparison.OrdinalIgnoreCase);

            if (isExit)
            {
                if (!ticket.CheckedInAt.HasValue || !ticket.IsInside)
                {
                    return new CheckinScanResponseDto
                    {
                        Success = false,
                        ResultCode = "InvalidExit",
                        TicketId = ticket.TicketId,
                        TicketTypeName = ticket.TicketTypeName,
                        OwnerName = ticket.OwnerName,
                        SeatId = ticket.SeatId,
                        Message = "Ticket has not been checked in yet or attendee is already outside."
                    };
                }

                ticket.IsInside = false;
                ticket.LastExitAt = DateTime.UtcNow;
                await _checkinRepository.AddStatusHistoryAsync(new StatusHistory
                {
                    TargetType = "Ticket",
                    TargetId = ticket.TicketId,
                    EventId = ticket.EventId,
                    TicketId = ticket.TicketId,
                    OrderId = ticket.OrderId,
                    ActorUserId = staffUserId,
                    ActorRole = "Staff",
                    Action = "Checkin_Exit",
                    OldStatus = ticket.Status,
                    NewStatus = ticket.Status,
                    Metadata = $"{{\"gate\":\"{request.GateName}\",\"action\":\"Exit\"}}",
                    CreatedAt = DateTime.UtcNow
                });
                await _checkinRepository.SaveChangesAsync();

                _logger.LogInformation("Ticket {TicketId} exit scanned at {Gate} by staff {StaffId}",
                    ticket.TicketId, request.GateName, staffUserId);

                string exitMsg;
                if (reEntryPolicy.Equals("TimeLimited", StringComparison.OrdinalIgnoreCase))
                {
                    var duration = eventInfo?.ReEntryDurationMinutes ?? 30;
                    exitMsg = $"Exit scan successful. Attendee has up to {duration} minutes to return.";
                }
                else if (reEntryPolicy.Equals("Unlimited", StringComparison.OrdinalIgnoreCase))
                {
                    exitMsg = "Exit scan successful. Attendee may re-enter anytime during the event.";
                }
                else
                {
                    exitMsg = "Exit scan successful. Note: Re-entry is not permitted for this event.";
                }

                return new CheckinScanResponseDto
                {
                    Success = true,
                    ResultCode = "ExitSuccess",
                    TicketId = ticket.TicketId,
                    TicketTypeName = ticket.TicketTypeName,
                    OwnerName = ticket.OwnerName,
                    SeatId = ticket.SeatId,
                    CheckedInAt = ticket.CheckedInAt,
                    CheckedInGate = ticket.CheckedInGate,
                    ReEntryPolicy = reEntryPolicy,
                    RemainingMinutes = eventInfo?.ReEntryDurationMinutes,
                    Message = exitMsg
                };
            }

            // Entry scan
            if (!ticket.CheckedInAt.HasValue)
            {
                // First time check-in
                var prevStatus = ticket.Status;
                ticket.CheckedInAt = DateTime.UtcNow;
                ticket.CheckedInBy = staffUserId;
                ticket.CheckedInGate = string.IsNullOrWhiteSpace(request.GateName) ? "Main Gate" : request.GateName;
                ticket.IsInside = true;
                ticket.Status = "Used";

                await _checkinRepository.AddStatusHistoryAsync(new StatusHistory
                {
                    TargetType = "Ticket",
                    TargetId = ticket.TicketId,
                    EventId = ticket.EventId,
                    TicketId = ticket.TicketId,
                    OrderId = ticket.OrderId,
                    ActorUserId = staffUserId,
                    ActorRole = "Staff",
                    Action = "Checkin_Entry",
                    OldStatus = prevStatus,
                    NewStatus = "Used",
                    Metadata = $"{{\"gate\":\"{ticket.CheckedInGate}\",\"action\":\"Entry\"}}",
                    CreatedAt = DateTime.UtcNow
                });

                await _checkinRepository.SaveChangesAsync();

                _logger.LogInformation("Ticket {TicketId} first check-in at {Gate} by staff {StaffId}",
                    ticket.TicketId, ticket.CheckedInGate, staffUserId);

                return new CheckinScanResponseDto
                {
                    Success = true,
                    ResultCode = "Valid",
                    TicketId = ticket.TicketId,
                    TicketTypeName = ticket.TicketTypeName,
                    OwnerName = ticket.OwnerName,
                    SeatId = ticket.SeatId,
                    CheckedInAt = ticket.CheckedInAt,
                    CheckedInGate = ticket.CheckedInGate,
                    ReEntryPolicy = reEntryPolicy,
                    Message = "Check-in successful. Welcome to the event!"
                };
            }

            // Already checked in previously
            if (ticket.IsInside)
            {
                // Anti-passback violation
                return new CheckinScanResponseDto
                {
                    Success = false,
                    ResultCode = "AntiPassback",
                    TicketId = ticket.TicketId,
                    TicketTypeName = ticket.TicketTypeName,
                    OwnerName = ticket.OwnerName,
                    SeatId = ticket.SeatId,
                    CheckedInAt = ticket.CheckedInAt,
                    CheckedInGate = ticket.CheckedInGate,
                    Message = $"Ticket was already scanned in at {ticket.CheckedInAt:HH:mm dd/MM/yyyy} ({ticket.CheckedInGate}) and is currently inside the venue."
                };
            }

            // Re-entry evaluation
            if (reEntryPolicy.Equals("Unlimited", StringComparison.OrdinalIgnoreCase))
            {
                ticket.IsInside = true;
                ticket.ReEntryCount++;
                await _checkinRepository.AddStatusHistoryAsync(new StatusHistory
                {
                    TargetType = "Ticket",
                    TargetId = ticket.TicketId,
                    EventId = ticket.EventId,
                    TicketId = ticket.TicketId,
                    OrderId = ticket.OrderId,
                    ActorUserId = staffUserId,
                    ActorRole = "Staff",
                    Action = "Checkin_ReEntry",
                    OldStatus = ticket.Status,
                    NewStatus = ticket.Status,
                    Metadata = $"{{\"gate\":\"{request.GateName}\",\"policy\":\"Unlimited\",\"count\":{ticket.ReEntryCount}}}",
                    CreatedAt = DateTime.UtcNow
                });
                await _checkinRepository.SaveChangesAsync();

                _logger.LogInformation("Ticket {TicketId} unlimited re-entry at {Gate} by staff {StaffId}",
                    ticket.TicketId, request.GateName, staffUserId);

                return new CheckinScanResponseDto
                {
                    Success = true,
                    ResultCode = "ValidReEntry",
                    TicketId = ticket.TicketId,
                    TicketTypeName = ticket.TicketTypeName,
                    OwnerName = ticket.OwnerName,
                    SeatId = ticket.SeatId,
                    CheckedInAt = ticket.CheckedInAt,
                    CheckedInGate = ticket.CheckedInGate,
                    ReEntryPolicy = reEntryPolicy,
                    Message = "Re-entry scan successful. Welcome back!"
                };
            }

            if (reEntryPolicy.Equals("TimeLimited", StringComparison.OrdinalIgnoreCase))
            {
                var allowedMinutes = eventInfo?.ReEntryDurationMinutes ?? 30;
                var exitTime = ticket.LastExitAt ?? ticket.CheckedInAt.Value;
                var elapsedMinutes = (DateTime.UtcNow - exitTime).TotalMinutes;

                if (elapsedMinutes > allowedMinutes)
                {
                    return new CheckinScanResponseDto
                    {
                        Success = false,
                        ResultCode = "OutsideTimeLimit",
                        TicketId = ticket.TicketId,
                        TicketTypeName = ticket.TicketTypeName,
                        OwnerName = ticket.OwnerName,
                        SeatId = ticket.SeatId,
                        CheckedInAt = ticket.CheckedInAt,
                        CheckedInGate = ticket.CheckedInGate,
                        ReEntryPolicy = reEntryPolicy,
                        Message = $"Re-entry time limit exceeded ({allowedMinutes} minutes). Attendee departed {(int)elapsedMinutes} minutes ago. Re-entry denied."
                    };
                }

                ticket.IsInside = true;
                ticket.ReEntryCount++;
                await _checkinRepository.AddStatusHistoryAsync(new StatusHistory
                {
                    TargetType = "Ticket",
                    TargetId = ticket.TicketId,
                    EventId = ticket.EventId,
                    TicketId = ticket.TicketId,
                    OrderId = ticket.OrderId,
                    ActorUserId = staffUserId,
                    ActorRole = "Staff",
                    Action = "Checkin_ReEntry",
                    OldStatus = ticket.Status,
                    NewStatus = ticket.Status,
                    Metadata = $"{{\"gate\":\"{request.GateName}\",\"policy\":\"Unlimited\",\"count\":{ticket.ReEntryCount}}}",
                    CreatedAt = DateTime.UtcNow
                });
                await _checkinRepository.SaveChangesAsync();

                _logger.LogInformation("Ticket {TicketId} time-limited re-entry ({Elapsed}/{Allowed} min) at {Gate} by staff {StaffId}",
                    ticket.TicketId, (int)elapsedMinutes, allowedMinutes, request.GateName, staffUserId);

                return new CheckinScanResponseDto
                {
                    Success = true,
                    ResultCode = "ValidReEntry",
                    TicketId = ticket.TicketId,
                    TicketTypeName = ticket.TicketTypeName,
                    OwnerName = ticket.OwnerName,
                    SeatId = ticket.SeatId,
                    CheckedInAt = ticket.CheckedInAt,
                    CheckedInGate = ticket.CheckedInGate,
                    ReEntryPolicy = reEntryPolicy,
                    RemainingMinutes = (int)(allowedMinutes - elapsedMinutes),
                    Message = $"Re-entry scan successful within the allowed window ({allowedMinutes} minutes)."
                };
            }

            // Default: NoReEntry
            return new CheckinScanResponseDto
            {
                Success = false,
                ResultCode = "AlreadyUsed",
                TicketId = ticket.TicketId,
                TicketTypeName = ticket.TicketTypeName,
                OwnerName = ticket.OwnerName,
                SeatId = ticket.SeatId,
                CheckedInAt = ticket.CheckedInAt,
                CheckedInGate = ticket.CheckedInGate,
                ReEntryPolicy = reEntryPolicy,
                Message = $"Ticket has already been used at {ticket.CheckedInAt:HH:mm dd/MM/yyyy} ({ticket.CheckedInGate}). Re-entry is not permitted for this event."
            };
        }
    }
}
