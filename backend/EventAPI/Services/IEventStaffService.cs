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

        // Phan cong mot nhan vien vao lam viec tai concert kem cong soat ve
        Task<EventStaffDTO> AssignAsync(int eventId, int staffUserId, string? gateName, int callerId, bool isAdmin, string? bearer, bool canReviewReturns = false);

        // Cap nhat quyen duyet hoan ve cho nhan vien tai su kien
        Task<EventStaffDTO> SetReturnReviewAsync(int eventId, int staffUserId, bool canReviewReturns, int callerId, bool isAdmin, string? bearer);

        // Lay danh sach eventId ma nhan vien co quyen duyet hoan ve
        Task<List<int>> GetReturnReviewEventIdsAsync(int staffUserId);

        // Lay danh sach concert va cong soat ve ma nhan vien duoc phan cong
        Task<List<StaffEventAssignmentDTO>> GetAssignmentsByStaffAsync(int staffUserId);

        // Huy phan cong nhan vien khoi concert
        Task UnassignAsync(int eventId, int staffUserId, int callerId, bool isAdmin);
    }
}
