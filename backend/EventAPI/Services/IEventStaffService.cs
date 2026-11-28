using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface IEventStaffService
    {
        Task<List<EventStaffDTO>> ListAsync(int eventId, int callerId, bool isAdmin, string? bearer);

        Task<List<StaffCandidateDTO>> SearchCandidatesAsync(int eventId, int callerId, bool isAdmin, string? bearer, string? query);

        Task<EventStaffDTO> AssignAsync(int eventId, int staffUserId, string? gateName, int callerId, bool isAdmin, string? bearer, bool canReviewReturns = false, string? shiftStartTime = null, string? shiftEndTime = null, string? shiftDate = null, bool canManageIncidents = false, bool canManageSupportCases = false, bool canCheckin = true, string? shiftEndDate = null);

        Task<EventStaffDTO> UpdateShiftAsync(int eventId, int staffUserId, string? gateName, string? shiftStartTime, string? shiftEndTime, int callerId, bool isAdmin, string? bearer, string? shiftDate = null, string? shiftEndDate = null);

        Task<EventStaffDTO> SetReturnReviewAsync(int eventId, int staffUserId, bool canReviewReturns, int callerId, bool isAdmin, string? bearer);

        Task<EventStaffDTO> UpdateDutiesAsync(int eventId, int staffUserId, UpdateStaffDutiesDTO dto, int callerId, bool isAdmin, string? bearer);

        Task<EventStaffDTO> SetDutyAsync(int eventId, int staffUserId, string dutyType, bool enabled, int callerId, bool isAdmin, string? bearer);

        // Event ids where this staff member may review ticket returns (used by TicketAPI).
        Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId);

        Task<List<StaffEventAssignmentDTO>> GetAssignmentsByStaffAsync(int staffUserId);

        Task UnassignAsync(int eventId, int staffUserId, int callerId, bool isAdmin);
    }
}
