using EventAPI.DTOs;
using static EventAPI.DTOs.ConfirmEventSaleDTO;

namespace EventAPI.Services
{
    public interface IEventService
    {
        // Kiem tra tinh hop le va kha dung cua slug khi nguoi dung nhap lieu
        Task<SlugAvailabilityDTO> CheckSlugAvailabilityAsync(string slug, int? excludeEventId = null);

        // Tao moi concert o trang thai Draft danh cho Organizer
        Task<EventResponseDTO> CreateAsync(CreateEventDTO dto, int organizerId);

        // Lay chi tiet concert theo id danh cho cong chung
        Task<EventResponseDTO> GetByIdAsync(int id, bool incrementView = false, bool publicOnly = true);

        // Lay chi tiet concert theo slug danh cho cong chung
        Task<EventResponseDTO> GetBySlugAsync(string slug, bool incrementView = false, bool publicOnly = true);

        // Tim kiem va loc danh sach concert theo nhieu tieu chi
        Task<PagedResultDTO<EventListDTO>> GetFilteredAsync(EventFilterDTO filter);

        // Lay danh sach concert noi bat hien thi o trang chu
        Task<List<EventListDTO>> GetFeaturedAsync(int count);

        // Lay danh sach concert thuoc so huu cua mot Organizer
        Task<List<EventListDTO>> GetByOrganizerIdAsync(int organizerId);

        // Lay thong tin tong quan dashboard ve tinh trang ban ve cua Organizer
        Task<OrganizerDashboardSummaryDTO> GetOrganizerDashboardAsync(int organizerId);

        // Lay hang doi moderation danh cho Admin kiem duyet
        Task<PagedResultDTO<EventListDTO>> GetModerationQueueAsync(int page, int pageSize);

        // Chinh sua thong tin concert o trang thai Draft hoac Rejected
        Task<EventResponseDTO> UpdateAsync(int id, UpdateEventDTO dto, int callerId, bool isAdmin);

        // Xoa mem concert o trang thai Draft hoac Rejected
        Task SoftDeleteAsync(int id, int callerId, bool isAdmin);

        // Khoi phuc concert da bi xoa mem danh cho Admin
        Task RestoreAsync(int id);

        // Lay chi tiet day du concert thuoc so huu cua Organizer o moi trang thai
        Task<EventResponseDTO> GetMineByIdAsync(int id, int callerId, bool isAdmin);

        // Kiem tra thu cac dieu kien cong bo truoc khi submit concert
        Task<SubmitValidationResultDTO> ValidateForSubmissionAsync(int id, int callerId, bool isAdmin);

        // Gui yeu cau xet duyet concert de chuyen tu Draft hoac Rejected sang Pending
        Task<EventResponseDTO> SubmitAsync(int id, int callerId);

        // Lay danh sach concert dang cho Admin phe duyet
        Task<PagedResultDTO<PendingEventDTO>> GetPendingAsync(int page, int pageSize);

        // Admin phe duyet concert sang trang thai Published de bat dau ban ve
        Task<(EventResponseDTO Event, GrantRoleResult RoleGrant)> ApproveAsync(
            int id, int adminId, string? adminBearerToken);

        // Admin tu choi duyet concert kem theo ly do chi tiet
        Task<EventResponseDTO> RejectAsync(int id, string reason, int adminId);

        // Huy su kien concert dang Pending hoac da Published
        Task<EventResponseDTO> CancelAsync(int id, int callerId, bool isAdmin);

        // Cap nhat anh poster cho concert
        Task<EventResponseDTO> SetPosterAsync(int id, string url, string publicId, int callerId, bool isAdmin);

        // Cap nhat anh banner cho concert
        Task<EventResponseDTO> SetBannerAsync(int id, string url, string publicId, int callerId, bool isAdmin);

        // Xoa anh poster cua concert khoi he thong va Cloudinary
        Task<EventResponseDTO> DeletePosterAsync(int id, int callerId, bool isAdmin);

        // Xoa anh banner cua concert khoi he thong va Cloudinary
        Task<EventResponseDTO> DeleteBannerAsync(int id, int callerId, bool isAdmin);

        // Lay entity concert va kiem tra quyen so huu cua caller
        Task<Models.Event> GetOwnedEntityAsync(int id, int callerId, bool isAdmin);

        // Xac nhan don hang da thanh toan thanh cong tu TicketAPI
        Task<bool> ConfirmPaidOrderSaleAsync(int eventId, ConfirmEventSaleRequestDTO request);

        // Tra lai ve vao kho sau khi hoan ve thanh cong
        Task<bool> ReleaseReturnedTicketAsync(int eventId, int ticketTypeId, int quantity);
    }
}
