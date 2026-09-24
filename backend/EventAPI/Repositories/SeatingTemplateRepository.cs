using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories
{
    public class SeatingTemplateRepository : ISeatingTemplateRepository
    {
        private readonly EventDbContext _context;

        public SeatingTemplateRepository(EventDbContext context)
        {
            _context = context;
        }

        public async Task<List<SeatingTemplate>> GetVisibleToAsync(int organizerId, bool isAdmin = false)
        {
            return await _context.SeatingTemplates
                .Where(t => !t.IsDeleted && (isAdmin || t.OrganizerId == organizerId || t.IsPublic))
                .OrderByDescending(t => t.UpdatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<SeatingTemplate?> GetByIdAsync(int seatingTemplateId)
        {
            return await _context.SeatingTemplates
                .FirstOrDefaultAsync(t => t.SeatingTemplateId == seatingTemplateId && !t.IsDeleted);
        }

        public async Task<SeatingTemplate> CreateAsync(SeatingTemplate entity)
        {
            _context.SeatingTemplates.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(SeatingTemplate entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _context.SeatingTemplates.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int seatingTemplateId)
        {
            var entity = await _context.SeatingTemplates.FindAsync(seatingTemplateId);
            if (entity != null)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
