using EventAPI.DTOs;

namespace EventAPI.Services
{
    // Dinh nghia cac phuong thuc quan ly nhan vien va phan cong tai concert
    public interface IEventStaffService
    {
        // Lay danh sach nhan vien duoc phan cong lam viec tai su kien
        Task<List<EventStaffDTO>> ListAsync(int eventId, int callerId, bool isAdmin, string? bearer);

        // Tim kiem nhan vien ung vien tu danh ba de phan cong
        Task<List<StaffCandidateDTO>> SearchCandidatesAsync(int eventId, int callerId, bool isAdmin, string? bearer, string? query);

        Task<EventStaffDTO> AssignAsync(int eventId, int staffUserId, string? gateName, int callerId, bool isAdmin, string? bearer, bool canReviewReturns = false, string? shiftStartTime = null, string? shiftEndTime = null, string? shiftDate = null, bool canManageIncidents = false, bool canManageSupportCases = false, bool canCheckin = true);

        Task<EventStaffDTO> UpdateShiftAsync(int eventId, int staffUserId, string? gateName, string? shiftStartTime, string? shiftEndTime, int callerId, bool isAdmin, string? bearer, string? shiftDate = null);

        // Cap nhat quyen duyet hoan ve cho nhan vien tai su kien
        Task<EventStaffDTO> SetReturnReviewAsync(int eventId, int staffUserId, bool canReviewReturns, int callerId, bool isAdmin, string? bearer);

        Task<EventStaffDTO> UpdateDutiesAsync(int eventId, int staffUserId, UpdateStaffDutiesDTO dto, int callerId, bool isAdmin, string? bearer);

        Task<EventStaffDTO> SetDutyAsync(int eventId, int staffUserId, string dutyType, bool enabled, int callerId, bool isAdmin, string? bearer);

        // Event ids where this staff member may review ticket returns (used by TicketAPI).
        Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId);

        // Lay danh sach concert va cong soat ve ma nhan vien duoc phan cong
        Task<List<StaffEventAssignmentDTO>> GetAssignmentsByStaffAsync(int staffUserId);

        // Huy phan cong nhan vien khoi concert
        Task UnassignAsync(int eventId, int staffUserId, int callerId, bool isAdmin);
    }
}
