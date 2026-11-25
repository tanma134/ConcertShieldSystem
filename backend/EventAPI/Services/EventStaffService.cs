using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Services
{
    // (view staff list) and (assign / unassign staff).
    // Only the event's organizer (or an Admin) may use it, and only real Staff accounts can be assigned.
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

        // the staff currently assigned to the event, oldest assignment first.
        // Lay danh sach nhan vien duoc phan cong lam viec tai su kien
        public async Task<List<EventStaffDTO>> ListAsync(int eventId, int callerId, bool isAdmin, string? bearer)
        {
            // Luu y: Kiem tra quyen so huu cua Organizer va lay day du thong tin tu Staff Directory
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var rows = await _staffRepository.GetActiveByEventIdAsync(eventId);

            var people = await _directory.GetByIdsAsync(rows.Select(r => r.StaffUserId), bearer);

            return rows.Select(row =>
            {
                var person = people.FirstOrDefault(p => p.UserId == row.StaffUserId);
                return ToDto(row, person);
            }).ToList();
        }

        // Staff accounts matching the text, each marked when already assigned.
        public async Task<List<StaffCandidateDTO>> SearchCandidatesAsync(
            int eventId, int callerId, bool isAdmin, string? bearer, string? query)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin);

            var assigned = await _staffRepository.GetActiveStaffUserIdsByEventIdAsync(eventId);

            var people = await _directory.SearchAsync(query, bearer);

            return people.Select(p => new StaffCandidateDTO
            {
                UserId = p.UserId,
                FullName = p.FullName,
                Email = p.Email,
                IsAssigned = assigned.Contains(p.UserId)
            }).ToList();
        }

        // assigns a Staff account to the event.
        // Phan cong mot nhan vien vao lam viec tai concert kem cong soat ve
        public async Task<EventStaffDTO> AssignAsync(int eventId, int staffUserId, string? gateName, int callerId, bool isAdmin, string? bearer, bool canReviewReturns = false)
        {
            // Luu y: Kiem tra nhan vien hop le tu Directory va ngan chan phan cong trung lap
            if (staffUserId <= 0)
                throw new ArgumentException("Please choose a staff member.");

            var ev = await EnsureOwnerAsync(eventId, callerId, isAdmin);

            if (EventStatus.Normalize(ev.Status) == EventStatus.Cancelled)
                throw new InvalidOperationException("Staff cannot be assigned to a cancelled concert.");

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault()
                ?? throw new InvalidOperationException("This account does not hold the Staff role.");

            var alreadyAssigned = await _staffRepository.IsStaffAssignedAsync(eventId, staffUserId);
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

            await _staffRepository.AddAsync(row);

            try
            {
                await _staffRepository.SaveChangesAsync();
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

            var row = await _staffRepository.GetActiveAssignmentAsync(eventId, staffUserId)
                ?? throw new KeyNotFoundException("This staff member is not assigned to the concert.");

            row.CanReviewReturns = canReviewReturns;
            await _staffRepository.SaveChangesAsync();

            var person = (await _directory.GetByIdsAsync(new[] { staffUserId }, bearer)).FirstOrDefault();
            return ToDto(row, person);
        }

        // Events where the staff member is actively assigned with the return-review duty.
        public async Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId)
        {
            return await _staffRepository.GetReturnReviewEventIdsAsync(staffUserId);
        }

        // Returns active event and gate assignments for the given staff member.
        public async Task<List<StaffEventAssignmentDTO>> GetAssignmentsByStaffAsync(int staffUserId)
        {
            return await _staffRepository.GetAssignmentsByStaffAsync(staffUserId);
        }

        // removes a staff member from the event; the row is kept as history.
        // Huy phan cong nhan vien khoi concert
        public async Task UnassignAsync(int eventId, int staffUserId, int callerId, bool isAdmin)
        {
            // Luu y: Xoa ban ghi phan cong de thu hoi quyen soat ve cua nhan vien
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
