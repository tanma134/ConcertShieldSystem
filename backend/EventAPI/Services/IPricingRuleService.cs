using EventAPI.DTOs;

namespace EventAPI.Services
{
    /// <summary>UC_27.1 — Configure Pricing Rule (dynamic pricing on top of a TicketType).</summary>
    public interface IPricingRuleService
    {
        Task<List<PricingRuleResponseDTO>> GetByTicketTypeIdAsync(int ticketTypeId, int? callerId, bool isAdmin);
        Task<PricingRuleResponseDTO> CreateAsync(int ticketTypeId, CreatePricingRuleDTO dto, int callerId, bool isAdmin);
        Task<PricingRuleResponseDTO> UpdateAsync(int pricingRuleId, UpdatePricingRuleDTO dto, int callerId, bool isAdmin);
        Task DeleteAsync(int pricingRuleId, int callerId, bool isAdmin);
    }
}
