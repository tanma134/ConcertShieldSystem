using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories
{
    public class RefundPolicyRepository : IRefundPolicyRepository
    {
        private readonly EventDbContext _context;

        public RefundPolicyRepository(EventDbContext context)
        {
            _context = context;
        }

        public async Task<List<RefundPolicy>> GetByEventIdAsync(int eventId, bool activeOnly = true)
        {
            var query = _context.RefundPolicies.Where(r => r.EventId == eventId);

            if (activeOnly)
                query = query.Where(r => r.IsActive);

            return await query
                .OrderByDescending(r => r.DeadlineBeforeEventHours)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<RefundPolicy?> GetByIdAsync(int refundPolicyId)
        {
            return await _context.RefundPolicies
                .FirstOrDefaultAsync(r => r.RefundPolicyId == refundPolicyId);
        }

        public async Task<RefundPolicy> CreateAsync(RefundPolicy entity)
        {
            _context.RefundPolicies.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(RefundPolicy entity)
        {
            _context.RefundPolicies.Update(entity);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// RefundPolicy has no soft-delete columns in the schema, so removal is a
        /// hard delete. Safe because policies are only editable while the concert is
        /// Draft/Rejected (enforced in the service layer) — i.e. before any ticket
        /// has ever been sold against them.
        /// </summary>
        public async Task DeleteAsync(int refundPolicyId)
        {
            var entity = await _context.RefundPolicies.FindAsync(refundPolicyId);
            if (entity != null)
            {
                _context.RefundPolicies.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
