using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Services
{
    // UC_14.3 (view staff list) and UC_14.4 (assign / unassign staff).
    // Only the event's organizer (or an Admin) may use it, and only real Staff accounts can be assigned.
    public class EventStaffService : IEventStaffService
    {
        private readonly EventDbContext _context;
        private readonly IEventRepository _events;
        private readonly IStaffDirectoryClient _directory;

        public EventStaffService(EventDbContext context, IEventRepository events, IStaffDirectoryClient directory)
        {
            _context = context;
            _events = events;
            _directory = directory;
        }

        // UC_14.3: the staff currently assigned to the event, oldest assignment first.
        public async Task<List<EventStaffDTO>> ListAsync(int eventId, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var rows = await _context.EventStaff
                .AsNoTracking()
                .Where(s => s.EventId == eventId && s.IsActive)
                .OrderBy(s => s.AssignedAt)
                .ToListAsync();

            var people = await _directory.GetByIdsAsync(rows.Select(r => r.StaffUserId), bearer);

            return rows.Select(row =>
            {
                var person = people.FirstOrDefault(p => p.UserId == row.StaffUserId);
                return ToDto(row, person);
            }).ToList();
        }

        // UC_14.4: Staff accounts matching the text, each marked when already assigned.
        public async Task<List<StaffCandidateDTO>> SearchCandidatesAsync(
            int eventId, int callerId, bool isAdmin, string? bearer, string? query)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var assigned = await _context.EventStaff
                .AsNoTracking()
                .Where(s => s.EventId == eventId && s.IsActive)
                .Select(s => s.StaffUserId)
                .ToListAsync();

            var people = await _directory.SearchAsync(query, bearer, isAdmin ? null : callerId);

            return people.Select(p => new StaffCandidateDTO
            {
                UserId = p.UserId,
                FullName = p.FullName,
                Email = p.Email,
                IsAssigned = assigned.Contains(p.UserId)
            }).ToList();
        }

        // UC_14.4: assigns a Staff account to the event.
        public async Task<EventStaffDTO> AssignAsync(
            int eventId, int staffUserId, string? gateName, int callerId, bool isAdmin, string? bearer,
            bool canReviewReturns = false, string? shiftStartTime = null, string? shiftEndTime = null, string? shiftDate = null,
            bool canManageIncidents = false, bool canManageSupportCases = false, bool canCheckin = true)
        {
            if (staffUserId <= 0)
                throw new ArgumentException("Please choose a staff member.");

            var ev = await EnsureOwnerAsync(eventId, callerId, isAdmin);

            if (EventStatus.Normalize(ev.Status) == EventStatus.Cancelled)
                throw new InvalidOperationException("Staff cannot be assigned to a cancelled concert.");

            if (!canCheckin && !canReviewReturns && !canManageIncidents && !canManageSupportCases)
            {
                throw new ArgumentException("Please assign at least one duty (Check-in, Return Review, Incidents, or Support Desk).");
            }

            // Timeline validation: ensure shift falls within event timeframe
            if (canCheckin && !string.IsNullOrWhiteSpace(shiftStartTime) && !string.IsNullOrWhiteSpace(shiftEndTime))
            {
                DateTime? startDt = null;
                DateTime? endDt = null;

                if (DateTime.TryParse(shiftStartTime, out var parsedStart)) startDt = parsedStart;
                if (DateTime.TryParse(shiftEndTime, out var parsedEnd)) endDt = parsedEnd;

                if (!startDt.HasValue && !string.IsNullOrWhiteSpace(shiftDate) && TimeSpan.TryParse(shiftStartTime, out var sTime))
                {
                    if (DateTime.TryParse(shiftDate, out var bDate)) startDt = bDate.Date.Add(sTime);
                }
                if (!endDt.HasValue && !string.IsNullOrWhiteSpace(shiftDate) && TimeSpan.TryParse(shiftEndTime, out var eTime))
                {
                    if (DateTime.TryParse(shiftDate, out var bDate)) endDt = bDate.Date.Add(eTime);
                }

                if (startDt.HasValue && endDt.HasValue)
                {
                    if (endDt.Value <= startDt.Value)
                    {
                        throw new ArgumentException("Shift end time must be after shift start time.");
                    }
                }
            }

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault()
                ?? throw new InvalidOperationException("This account does not hold the Staff role.");

            var alreadyAssigned = await _context.EventStaff
                .AnyAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive);
            if (alreadyAssigned)
                throw new InvalidOperationException("This staff member is already assigned to the concert.");

            DateOnly? parsedShiftDate = null;
            if (!string.IsNullOrWhiteSpace(shiftDate) && DateOnly.TryParse(shiftDate, out var sDate))
            {
                parsedShiftDate = sDate;
            }
            else
            {
                parsedShiftDate = DateOnly.FromDateTime(ev.StartsAt);
            }

            var row = new EventStaff
            {
                EventId = eventId,
                StaffUserId = staffUserId,
                GateName = string.IsNullOrWhiteSpace(gateName) ? "Main Gate" : gateName.Trim(),
                ShiftDate = parsedShiftDate,
                ShiftStartTime = string.IsNullOrWhiteSpace(shiftStartTime) ? null : shiftStartTime.Trim(),
                ShiftEndTime = string.IsNullOrWhiteSpace(shiftEndTime) ? null : shiftEndTime.Trim(),
                AssignedBy = callerId,
                AssignedAt = DateTime.UtcNow,
                IsActive = true,
                CanCheckin = canCheckin,
                CanReviewReturns = canReviewReturns,
                CanManageIncidents = canManageIncidents,
                CanManageSupportCases = canManageSupportCases
            };

            _context.EventStaff.Add(row);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Two clicks at once: the unique index let only one of them through.
                if (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
                {
                    throw new InvalidOperationException("This staff member is already assigned to the concert.");
                }
                throw;
            }

            return ToDto(row, person);
        }

        // Updates gate and shift time of an assigned staff member.
        public async Task<EventStaffDTO> UpdateShiftAsync(
            int eventId, int staffUserId, string? gateName, string? shiftStartTime, string? shiftEndTime, int callerId, bool isAdmin, string? bearer, string? shiftDate = null)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _context.EventStaff
                .FirstOrDefaultAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            if (!string.IsNullOrWhiteSpace(shiftStartTime) && !string.IsNullOrWhiteSpace(shiftEndTime))
            {
                var ev = await _context.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
                if (ev != null)
                {
                    var targetDate = !string.IsNullOrWhiteSpace(shiftDate) && DateOnly.TryParse(shiftDate, out var sD)
                        ? sD
                        : (row.ShiftDate ?? DateOnly.FromDateTime(ev.StartsAt));

                    if (TimeSpan.TryParse(shiftStartTime, out var sTime) && TimeSpan.TryParse(shiftEndTime, out var eTime))
                    {
                        var startDt = targetDate.ToDateTime(TimeOnly.FromTimeSpan(sTime), DateTimeKind.Utc);
                        var endDt = targetDate.ToDateTime(TimeOnly.FromTimeSpan(eTime), DateTimeKind.Utc);

                        if (endDt <= startDt)
                        {
                            throw new ArgumentException("Shift end time must be after shift start time.");
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(gateName)) row.GateName = gateName.Trim();
            if (shiftStartTime != null) row.ShiftStartTime = string.IsNullOrWhiteSpace(shiftStartTime) ? null : shiftStartTime.Trim();
            if (shiftEndTime != null) row.ShiftEndTime = string.IsNullOrWhiteSpace(shiftEndTime) ? null : shiftEndTime.Trim();
            if (!string.IsNullOrWhiteSpace(shiftDate) && DateOnly.TryParse(shiftDate, out var sDate))
            {
                row.ShiftDate = sDate;
            }

            await _context.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        // Organizer decides whether an assigned staff member may review ticket returns of this event.
        public async Task<EventStaffDTO> SetReturnReviewAsync(
            int eventId, int staffUserId, bool canReviewReturns, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _context.EventStaff
                .FirstOrDefaultAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            row.CanReviewReturns = canReviewReturns;
            await _context.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        public async Task<EventStaffDTO> UpdateDutiesAsync(
            int eventId, int staffUserId, UpdateStaffDutiesDTO dto, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _context.EventStaff
                .FirstOrDefaultAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            if (dto.CanCheckin.HasValue) row.CanCheckin = dto.CanCheckin.Value;
            if (dto.CanReviewReturns.HasValue) row.CanReviewReturns = dto.CanReviewReturns.Value;
            if (dto.CanManageIncidents.HasValue) row.CanManageIncidents = dto.CanManageIncidents.Value;
            if (dto.CanManageSupportCases.HasValue) row.CanManageSupportCases = dto.CanManageSupportCases.Value;

            await _context.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        public async Task<EventStaffDTO> SetDutyAsync(
            int eventId, int staffUserId, string dutyType, bool enabled, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _context.EventStaff
                .FirstOrDefaultAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            switch (dutyType.Trim().ToLowerInvariant())
            {
                case "checkin":
                case "cancheckin":
                case "scan":
                case "scanning":
                    row.CanCheckin = enabled;
                    break;
                case "returns":
                case "canreviewreturns":
                case "returnreview":
                    row.CanReviewReturns = enabled;
                    break;
                case "incidents":
                case "canmanageincidents":
                case "incident":
                    row.CanManageIncidents = enabled;
                    break;
                case "support":
                case "canmanagesupportcases":
                case "supportcase":
                case "supportcases":
                    row.CanManageSupportCases = enabled;
                    break;
                default:
                    throw new ArgumentException($"Unknown duty type: {dutyType}");
            }

            await _context.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        // Events where the staff member is actively assigned with the return-review duty.
        public async Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId)
        {
            return await _context.EventStaff
                .AsNoTracking()
                .Where(s => s.StaffUserId == staffUserId && s.IsActive && s.CanReviewReturns)
                .Select(s => s.EventId)
                .Distinct()
                .ToListAsync();
        }

        // Returns active event and gate assignments for the given staff member.
        public async Task<List<StaffEventAssignmentDTO>> GetAssignmentsByStaffAsync(int staffUserId)
        {
            var query = from s in _context.EventStaff.AsNoTracking()
                        where s.StaffUserId == staffUserId && s.IsActive
                        join e in _context.Events.AsNoTracking() on s.EventId equals e.EventId
                        where !e.IsDeleted
                        orderby s.AssignedAt descending
                        select new StaffEventAssignmentDTO
                        {
                            EventStaffId = s.EventStaffId,
                            EventId = s.EventId,
                            StaffUserId = s.StaffUserId,
                            GateName = s.GateName,
                            ShiftDate = s.ShiftDate.HasValue ? s.ShiftDate.Value.ToString("yyyy-MM-dd") : e.StartsAt.ToString("yyyy-MM-dd"),
                            ShiftStartTime = s.ShiftStartTime,
                            ShiftEndTime = s.ShiftEndTime,
                            EventTitle = e.Title,
                            City = e.City ?? string.Empty,
                            LocationName = e.LocationName ?? e.Address ?? string.Empty,
                            StartsAt = e.StartsAt,
                            EndsAt = e.EndsAt,
                            Status = e.Status,
                            CanCheckin = s.CanCheckin,
                            CanReviewReturns = s.CanReviewReturns,
                            CanManageIncidents = s.CanManageIncidents,
                            CanManageSupportCases = s.CanManageSupportCases,
                            AssignedAt = s.AssignedAt
                        };

            return await query.ToListAsync();
        }

        // UC_14.4: removes a staff member from the event; the row is kept as history.
        public async Task UnassignAsync(int eventId, int staffUserId, int callerId, bool isAdmin)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _context.EventStaff
                .FirstOrDefaultAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            row.IsActive = false;
            row.UnassignedBy = callerId;
            row.UnassignedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        // Loads the event and checks the caller owns it. A stranger gets the same
        // "not found" as a missing concert, so ids cannot be probed.
        private async Task<Event> EnsureOwnerAsync(int eventId, int callerId, bool isAdmin)
        {
            var ev = await _events.GetByIdAsync(eventId)
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new KeyNotFoundException($"Event {eventId} not found.");

            return ev;
        }

        // Combines the assignment row with the account details from AuthenticationAPI.
        private static EventStaffDTO ToDto(EventStaff row, StaffDirectoryEntry? person)
        {
            return new EventStaffDTO
            {
                EventStaffId = row.EventStaffId,
                EventId = row.EventId,
                StaffUserId = row.StaffUserId,
                FullName = person?.FullName ?? "Unavailable account",
                Email = person?.Email ?? string.Empty,
                GateName = row.GateName,
                ShiftDate = row.ShiftDate.HasValue ? row.ShiftDate.Value.ToString("yyyy-MM-dd") : null,
                ShiftStartTime = row.ShiftStartTime,
                ShiftEndTime = row.ShiftEndTime,
                CanCheckin = row.CanCheckin,
                CanReviewReturns = row.CanReviewReturns,
                CanManageIncidents = row.CanManageIncidents,
                CanManageSupportCases = row.CanManageSupportCases,
                AssignedAt = row.AssignedAt,
                AssignedBy = row.AssignedBy
            };
        }
    }
}
