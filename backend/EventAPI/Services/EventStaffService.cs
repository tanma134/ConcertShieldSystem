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

            var people = await _directory.SearchAsync(query, bearer);

            return people.Select(p => new StaffCandidateDTO
            {
                UserId = p.UserId,
                FullName = p.FullName,
                Email = p.Email,
                IsAssigned = assigned.Contains(p.UserId)
            }).ToList();
        }

        // UC_14.4: assigns a Staff account to the event.
        public async Task<EventStaffDTO> AssignAsync(int eventId, int staffUserId, string? gateName, int callerId, bool isAdmin, string? bearer, bool canReviewReturns = false)
        {
            if (staffUserId <= 0)
                throw new ArgumentException("Please choose a staff member.");

            var ev = await EnsureOwnerAsync(eventId, callerId, isAdmin);

            if (EventStatus.Normalize(ev.Status) == EventStatus.Cancelled)
                throw new InvalidOperationException("Staff cannot be assigned to a cancelled concert.");

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault()
                ?? throw new InvalidOperationException("This account does not hold the Staff role.");

            var alreadyAssigned = await _context.EventStaff
                .AnyAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive);
            if (alreadyAssigned)
                throw new InvalidOperationException("This staff member is already assigned to the concert.");

            var row = new EventStaff
            {
                EventId = eventId,
                StaffUserId = staffUserId,
                GateName = string.IsNullOrWhiteSpace(gateName) ? "Main Gate" : gateName.Trim(),
                AssignedBy = callerId,
                AssignedAt = DateTime.UtcNow,
                IsActive = true,
                CanReviewReturns = canReviewReturns
            };

            _context.EventStaff.Add(row);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two clicks at once: the unique index let only one of them through.
                throw new InvalidOperationException("This staff member is already assigned to the concert.");
            }

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
                CanReviewReturns = row.CanReviewReturns,
                AssignedAt = row.AssignedAt,
                AssignedBy = row.AssignedBy
            };
        }
    }
}
