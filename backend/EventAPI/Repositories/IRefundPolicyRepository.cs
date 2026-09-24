using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface IRefundPolicyRepository
    {
        Task<List<RefundPolicy>> GetByEventIdAsync(int eventId, bool activeOnly = true);
        Task<RefundPolicy?> GetByIdAsync(int refundPolicyId);
        Task<RefundPolicy> CreateAsync(RefundPolicy entity);
        Task UpdateAsync(RefundPolicy entity);
        Task DeleteAsync(int refundPolicyId);
    }
}
