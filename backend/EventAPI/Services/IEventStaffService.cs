using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface IEventStaffService
    {
        Task<List<EventStaffDTO>> ListAsync(int eventId, int callerId, bool isAdmin, string? bearer);

        Task<List<StaffCandidateDTO>> SearchCandidatesAsync(int eventId, int callerId, bool isAdmin, string? bearer, string? query);

        Task<EventStaffDTO> AssignAsync(int eventId, int staffUserId, string? gateName, int callerId, bool isAdmin, string? bearer, bool canReviewReturns = false);

        Task<EventStaffDTO> SetReturnReviewAsync(int eventId, int staffUserId, bool canReviewReturns, int callerId, bool isAdmin, string? bearer);

        // Event ids where this staff member may review ticket returns (used by TicketAPI).
        Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId);

        // Events and gates a staff member is assigned to work at.
        Task<List<StaffEventAssignmentDTO>> GetAssignmentsByStaffAsync(int staffUserId);

        Task UnassignAsync(int eventId, int staffUserId, int callerId, bool isAdmin);
    }
}
