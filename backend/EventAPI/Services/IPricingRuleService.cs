using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface IPricingRuleService
    {
        Task<List<PricingRuleResponseDTO>> GetByTicketTypeIdAsync(int ticketTypeId, int? callerId, bool isAdmin);

        Task<PricingRuleResponseDTO> CreateAsync(int ticketTypeId, CreatePricingRuleDTO dto, int callerId, bool isAdmin);

        Task<PricingRuleResponseDTO> UpdateAsync(int pricingRuleId, UpdatePricingRuleDTO dto, int callerId, bool isAdmin);

        Task DeleteAsync(int pricingRuleId, int callerId, bool isAdmin);
    }
}
