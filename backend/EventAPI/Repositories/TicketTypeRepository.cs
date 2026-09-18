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
    }
}
