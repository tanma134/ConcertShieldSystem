using EventAPI.DTOs;
using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface IEventStaffRepository
    {
        Task<List<EventStaff>> GetActiveByEventIdAsync(int eventId);
        Task<List<int>> GetActiveStaffUserIdsByEventIdAsync(int eventId);
        Task<bool> IsStaffAssignedAsync(int eventId, int staffUserId);
        Task<EventStaff?> GetActiveAssignmentAsync(int eventId, int staffUserId);
        Task AddAsync(EventStaff entity);
        Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId);
        Task<List<StaffEventAssignmentDTO>> GetAssignmentsByStaffAsync(int staffUserId);
        Task SaveChangesAsync();
    }
}

