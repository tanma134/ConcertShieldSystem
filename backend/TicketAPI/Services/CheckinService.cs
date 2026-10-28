using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;

namespace TicketAPI.Services
{
    public class CheckinService : ICheckinService
    {
        private readonly TicketDbContext _context;
        private readonly IEventApiClient _eventApiClient;
        private readonly ILogger<CheckinService> _logger;

        public CheckinService(
            TicketDbContext context,
            IEventApiClient eventApiClient,
            ILogger<CheckinService> logger)
        {
            _context = context;
            _eventApiClient = eventApiClient;
            _logger = logger;
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
                ticket = await _context.Tickets
                    .Include(t => t.Order)
                    .FirstOrDefaultAsync(t => t.TicketCode == request.TicketCode.Value && !t.IsDeleted);
            }
            else if (!string.IsNullOrWhiteSpace(request.QrToken))
            {
                if (Guid.TryParse(request.QrToken, out var parsedCode))
                {
                    ticket = await _context.Tickets
                        .Include(t => t.Order)
                        .FirstOrDefaultAsync(t => t.TicketCode == parsedCode && !t.IsDeleted);
                }

                if (ticket == null)
                {
                    var qrRecord = await _context.TicketQrTokens
                        .Include(q => q.Ticket)
                        .ThenInclude(t => t.Order)
                        .FirstOrDefaultAsync(q => q.QrToken == request.QrToken && !q.IsRevoked);

                    if (qrRecord != null && !qrRecord.Ticket.IsDeleted)
                    {
                        ticket = qrRecord.Ticket;
                    }
                }
            }
            else if (request.TicketId.HasValue)
            {
                ticket = await _context.Tickets
                    .Include(t => t.Order)
                    .FirstOrDefaultAsync(t => t.TicketId == request.TicketId.Value && !t.IsDeleted);
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

            if (ticket.Status != "Active" || (ticket.Order != null && ticket.Order.Status != "Paid"))
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
                await _context.SaveChangesAsync();

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
                ticket.CheckedInAt = DateTime.UtcNow;
                ticket.CheckedInBy = staffUserId;
                ticket.CheckedInGate = string.IsNullOrWhiteSpace(request.GateName) ? "Main Gate" : request.GateName;
                ticket.IsInside = true;

                await _context.SaveChangesAsync();

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
                await _context.SaveChangesAsync();

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
                await _context.SaveChangesAsync();

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
