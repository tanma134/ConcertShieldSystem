using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface IPricingRuleRepository
    {
        Task<List<PricingRule>> GetByTicketTypeIdAsync(int ticketTypeId);
        Task<PricingRule?> GetByIdAsync(int pricingRuleId);
        Task<PricingRule> CreateAsync(PricingRule entity);
        Task UpdateAsync(PricingRule entity);
        Task DeleteAsync(int pricingRuleId);
    }
}
