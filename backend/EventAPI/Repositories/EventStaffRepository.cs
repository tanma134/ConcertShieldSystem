using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories
{
    public class EventStaffRepository : IEventStaffRepository
    {
        private readonly EventDbContext _context;

        public EventStaffRepository(EventDbContext context)
        {
            _context = context;
        }

        public async Task<List<EventStaff>> GetActiveByEventIdAsync(int eventId)
        {
            return await _context.EventStaff
                .AsNoTracking()
                .Where(s => s.EventId == eventId && s.IsActive)
                .OrderBy(s => s.AssignedAt)
                .ToListAsync();
        }

        public async Task<List<int>> GetActiveStaffUserIdsByEventIdAsync(int eventId)
        {
            return await _context.EventStaff
                .AsNoTracking()
                .Where(s => s.EventId == eventId && s.IsActive)
                .Select(s => s.StaffUserId)
                .ToListAsync();
        }

        public async Task<bool> IsStaffAssignedAsync(int eventId, int staffUserId)
        {
            return await _context.EventStaff
                .AnyAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive);
        }

        public async Task<EventStaff?> GetActiveAssignmentAsync(int eventId, int staffUserId)
        {
            return await _context.EventStaff
                .FirstOrDefaultAsync(s => s.EventId == eventId && s.StaffUserId == staffUserId && s.IsActive);
        }

        public async Task AddAsync(EventStaff entity)
        {
            await _context.EventStaff.AddAsync(entity);
        }

        public async Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId)
        {
            return await _context.EventStaff
                .AsNoTracking()
                .Where(s => s.StaffUserId == staffUserId && s.IsActive && s.CanReviewReturns)
                .Select(s => s.EventId)
                .Distinct()
                .ToListAsync();
        }

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
                            EventTitle = e.Title,
                            City = e.City ?? string.Empty,
                            LocationName = e.LocationName ?? e.Address ?? string.Empty,
                            StartsAt = e.StartsAt,
                            EndsAt = e.EndsAt,
                            Status = e.Status,
                            CanReviewReturns = s.CanReviewReturns,
                            AssignedAt = s.AssignedAt
                        };

            return await query.ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}

