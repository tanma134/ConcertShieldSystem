using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface IRefundPolicyService
    {
        Task<List<RefundPolicyResponseDTO>> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin);
        Task<RefundPolicyResponseDTO> CreateAsync(int eventId, CreateRefundPolicyDTO dto, int callerId, bool isAdmin);
        Task<RefundPolicyResponseDTO> UpdateAsync(int refundPolicyId, UpdateRefundPolicyDTO dto, int callerId, bool isAdmin);
        Task DeleteAsync(int refundPolicyId, int callerId, bool isAdmin);
    }
}
