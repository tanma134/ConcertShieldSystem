using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories
{
    public class PricingRuleRepository : IPricingRuleRepository
    {
        private readonly EventDbContext _context;

        public PricingRuleRepository(EventDbContext context)
        {
            _context = context;
        }

        public async Task<List<PricingRule>> GetByTicketTypeIdAsync(int ticketTypeId)
        {
            return await _context.PricingRules
                .Where(r => r.TicketTypeId == ticketTypeId)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<PricingRule?> GetByIdAsync(int pricingRuleId)
        {
            return await _context.PricingRules
                .FirstOrDefaultAsync(r => r.PricingRuleId == pricingRuleId);
        }

        public async Task<PricingRule> CreateAsync(PricingRule entity)
        {
            _context.PricingRules.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(PricingRule entity)
        {
            _context.PricingRules.Update(entity);
            await _context.SaveChangesAsync();
        }

        // Hard-delete is safe because rules are only editable in Draft/Rejected before any sales occur.
        public async Task DeleteAsync(int pricingRuleId)
        {
            var entity = await _context.PricingRules.FindAsync(pricingRuleId);
            if (entity != null)
            {
                _context.PricingRules.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
