using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Services
{
    public class EventStaffService : IEventStaffService
    {
        private readonly IEventStaffRepository _staffRepository;
        private readonly IEventRepository _events;
        private readonly IStaffDirectoryClient _directory;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public EventStaffService(IEventStaffRepository staffRepository, IEventRepository events, IStaffDirectoryClient directory)
        {
            _staffRepository = staffRepository;
            _events = events;
            _directory = directory;
        }

        // Backward compatibility overload for existing tests
        public EventStaffService(EventDbContext context, IEventRepository events, IStaffDirectoryClient directory)
            : this(new EventStaffRepository(context), events, directory)
        {
        }

        public async Task<List<EventStaffDTO>> ListAsync(int eventId, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var rows = await _staffRepository.GetActiveByEventIdAsync(eventId);

            var people = await _directory.GetByIdsAsync(rows.Select(r => r.StaffUserId), bearer);

            return rows.Select(row =>
            {
                var person = people.FirstOrDefault(p => p.UserId == row.StaffUserId);
                return ToDto(row, person);
            }).ToList();
        }

        public async Task<List<StaffCandidateDTO>> SearchCandidatesAsync(
            int eventId, int callerId, bool isAdmin, string? bearer, string? query)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var assigned = await _staffRepository.GetActiveStaffUserIdsByEventIdAsync(eventId);

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
            bool canManageIncidents = false, bool canManageSupportCases = false, bool canCheckin = true, string? shiftEndDate = null)
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

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault()
                ?? throw new InvalidOperationException("This account does not hold the Staff role.");

            var alreadyAssigned = await _staffRepository.IsStaffAssignedAsync(eventId, staffUserId);
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

            DateOnly? parsedShiftEndDate = null;
            if (!string.IsNullOrWhiteSpace(shiftEndDate) && DateOnly.TryParse(shiftEndDate, out var seDate))
            {
                parsedShiftEndDate = seDate;
            }
            else
            {
                parsedShiftEndDate = parsedShiftDate;
            }

            var row = new EventStaff
            {
                EventId = eventId,
                StaffUserId = staffUserId,
                GateName = string.IsNullOrWhiteSpace(gateName) ? "Main Gate" : gateName.Trim(),
                ShiftDate = parsedShiftDate,
                ShiftEndDate = parsedShiftEndDate,
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

            await _staffRepository.AddAsync(row);

            try
            {
                await _staffRepository.SaveChangesAsync();
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
            int eventId, int staffUserId, string? gateName, string? shiftStartTime, string? shiftEndTime, int callerId, bool isAdmin, string? bearer, string? shiftDate = null, string? shiftEndDate = null)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _staffRepository.GetActiveAssignmentAsync(eventId, staffUserId)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            if (!string.IsNullOrWhiteSpace(gateName)) row.GateName = gateName.Trim();
            if (shiftStartTime != null) row.ShiftStartTime = string.IsNullOrWhiteSpace(shiftStartTime) ? null : shiftStartTime.Trim();
            if (shiftEndTime != null) row.ShiftEndTime = string.IsNullOrWhiteSpace(shiftEndTime) ? null : shiftEndTime.Trim();
            if (!string.IsNullOrWhiteSpace(shiftDate) && DateOnly.TryParse(shiftDate, out var sDate))
            {
                row.ShiftDate = sDate;
            }
            if (!string.IsNullOrWhiteSpace(shiftEndDate) && DateOnly.TryParse(shiftEndDate, out var seDate))
            {
                row.ShiftEndDate = seDate;
            }
            else if (!string.IsNullOrWhiteSpace(shiftDate))
            {
                row.ShiftEndDate = row.ShiftEndDate ?? row.ShiftDate;
            }

            await _staffRepository.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        // Organizer decides whether an assigned staff member may review ticket returns of this event.
        public async Task<EventStaffDTO> SetReturnReviewAsync(
            int eventId, int staffUserId, bool canReviewReturns, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _staffRepository.GetActiveAssignmentAsync(eventId, staffUserId)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            row.CanReviewReturns = canReviewReturns;
            await _staffRepository.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        public async Task<EventStaffDTO> UpdateDutiesAsync(
            int eventId, int staffUserId, UpdateStaffDutiesDTO dto, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _staffRepository.GetActiveAssignmentAsync(eventId, staffUserId)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            if (dto.CanCheckin.HasValue) row.CanCheckin = dto.CanCheckin.Value;
            if (dto.CanReviewReturns.HasValue) row.CanReviewReturns = dto.CanReviewReturns.Value;
            if (dto.CanManageIncidents.HasValue) row.CanManageIncidents = dto.CanManageIncidents.Value;
            if (dto.CanManageSupportCases.HasValue) row.CanManageSupportCases = dto.CanManageSupportCases.Value;

            await _staffRepository.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        public async Task<EventStaffDTO> SetDutyAsync(
            int eventId, int staffUserId, string dutyType, bool enabled, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _staffRepository.GetActiveAssignmentAsync(eventId, staffUserId)
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

            await _staffRepository.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        // Events where the staff member is actively assigned with the return-review duty.
        public async Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId)
        {
            return await _staffRepository.GetReturnReviewEventIdsAsync(staffUserId);
        }

        public async Task<List<StaffEventAssignmentDTO>> GetAssignmentsByStaffAsync(int staffUserId)
        {
            var assignments = await _staffRepository.GetAssignmentsByStaffAsync(staffUserId);
            // The repository's base projection predates shift and duty fields.
            foreach (var assignment in assignments)
            {
                var row = await _staffRepository.GetActiveAssignmentAsync(assignment.EventId, staffUserId);
                if (row == null) continue;
                assignment.ShiftDate = (row.ShiftDate ?? DateOnly.FromDateTime(assignment.StartsAt)).ToString("yyyy-MM-dd");
                assignment.ShiftEndDate = (row.ShiftEndDate ?? row.ShiftDate ?? DateOnly.FromDateTime(assignment.EndsAt)).ToString("yyyy-MM-dd");
                assignment.ShiftStartTime = row.ShiftStartTime;
                assignment.ShiftEndTime = row.ShiftEndTime;
                assignment.CanCheckin = row.CanCheckin;
                assignment.CanReviewReturns = row.CanReviewReturns;
                assignment.CanManageIncidents = row.CanManageIncidents;
                assignment.CanManageSupportCases = row.CanManageSupportCases;
            }
            return assignments;
        }

        public async Task UnassignAsync(int eventId, int staffUserId, int callerId, bool isAdmin)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var row = await _staffRepository.GetActiveAssignmentAsync(eventId, staffUserId)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            row.IsActive = false;
            row.UnassignedBy = callerId;
            row.UnassignedAt = DateTime.UtcNow;

            await _staffRepository.SaveChangesAsync();
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
                ShiftEndDate = row.ShiftEndDate.HasValue ? row.ShiftEndDate.Value.ToString("yyyy-MM-dd") : null,
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
