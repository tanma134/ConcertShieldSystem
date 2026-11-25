using EventAPI.DTOs;

namespace EventAPI.Services
{
    // Dinh nghia cac phuong thuc cau hinh chinh sach hoan ve cho concert
    public interface IRefundPolicyService
    {
        // Lay danh sach cac chinh sach hoan ve da cau hinh cua concert
        Task<List<RefundPolicyResponseDTO>> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin);

        // Tao moi chinh sach hoan ve voi moc thoi gian va phan tram hoan
        Task<RefundPolicyResponseDTO> CreateAsync(int eventId, CreateRefundPolicyDTO dto, int callerId, bool isAdmin);

        // Cap nhat moc thoi gian va ty le hoan ve cho su kien
        Task<RefundPolicyResponseDTO> UpdateAsync(int refundPolicyId, UpdateRefundPolicyDTO dto, int callerId, bool isAdmin);

        // Xoa chinh sach hoan ve khoi concert
        Task DeleteAsync(int refundPolicyId, int callerId, bool isAdmin);
    }
}
