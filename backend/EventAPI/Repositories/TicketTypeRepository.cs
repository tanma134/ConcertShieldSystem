using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories
{
    public class TicketTypeRepository : ITicketTypeRepository
    {
        private readonly EventDbContext _context;

        public TicketTypeRepository(EventDbContext context)
        {
            _context = context;
        }

        public async Task<List<TicketType>> GetByEventIdAsync(int eventId)
        {
            return await _context.TicketTypes
                .Where(t => t.EventId == eventId && !t.IsDeleted)
                .OrderBy(t => t.SortOrder)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<TicketType?> GetByIdAsync(int id)
        {
            return await _context.TicketTypes
                .FirstOrDefaultAsync(t => t.TicketTypeId == id && !t.IsDeleted);
        }

        public async Task<bool> ExistsNameAsync(int eventId, string typeName, int? excludeId = null)
        {
            var normalized = typeName.Trim().ToLower();
            return await _context.TicketTypes.AsNoTracking().AnyAsync(t =>
                t.EventId == eventId && !t.IsDeleted &&
                t.TypeName.Trim().ToLower() == normalized &&
                (!excludeId.HasValue || t.TicketTypeId != excludeId.Value));
        }

        public async Task<TicketType> CreateAsync(TicketType entity)
        {
            _context.TicketTypes.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(TicketType entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _context.TicketTypes.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id, int deletedBy)
        {
            var entity = await _context.TicketTypes.FindAsync(id);
            if (entity != null)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.DeletedBy = deletedBy;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> TryReserveAsync(int ticketTypeId, int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "quantity must be greater than 0.");

            // One conditional UPDATE = one atomic read-check-write at the database.
            // The WHERE clause re-checks capacity against the row Postgres has just
            // locked, so this is race-safe under arbitrary concurrent callers without
            // any application-level lock, SELECT ... FOR UPDATE, or Redis lock.
            var rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ticket_types
                SET sold_quantity = sold_quantity + {quantity},
                    updated_at = now()
                WHERE ticket_type_id = {ticketTypeId}
                  AND is_deleted = false
                  AND status = 'Active'
                  AND sold_quantity + {quantity} <= quantity");

            return rows > 0;
        }

        public async Task<bool> ReleaseAsync(int ticketTypeId, int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "quantity must be greater than 0.");

            // GREATEST(...,0) makes this safe to call twice for the same release
            // (duplicate refund/cancel event, retried webhook) without going negative,
            // which would otherwise trip ck_ticket_types_sold_within_quantity the other
            // way or silently under-count sales.
            var rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ticket_types
                SET sold_quantity = GREATEST(sold_quantity - {quantity}, 0),
                    updated_at = now()
                WHERE ticket_type_id = {ticketTypeId}
                  AND is_deleted = false");

            return rows > 0;
        }
    }
}
