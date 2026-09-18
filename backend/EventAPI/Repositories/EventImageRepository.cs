using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories
{
    public class EventImageRepository : IEventImageRepository
    {
        private readonly EventDbContext _context;

        public EventImageRepository(EventDbContext context)
        {
            _context = context;
        }

        public async Task<List<EventImage>> GetByEventIdAsync(int eventId)
        {
            return await _context.EventImages
                .Where(i => i.EventId == eventId && !i.IsDeleted)
                .OrderBy(i => i.SortOrder)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<EventImage?> GetByIdAsync(int id)
        {
            return await _context.EventImages
                .FirstOrDefaultAsync(i => i.ImageId == id && !i.IsDeleted);
        }

        public async Task<EventImage> CreateAsync(EventImage entity)
        {
            _context.EventImages.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(EventImage entity)
        {
            _context.EventImages.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id, int deletedBy)
        {
            var entity = await _context.EventImages.FindAsync(id);
            if (entity != null)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.DeletedBy = deletedBy;
                await _context.SaveChangesAsync();
            }
        }

        public async Task SetMainAsync(int eventId, int imageId)
        {
            // Unset all main images for the event
            await _context.EventImages
                .Where(i => i.EventId == eventId && !i.IsDeleted)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsMain, false));

            // Set the specified image as main
            await _context.EventImages
                .Where(i => i.ImageId == imageId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsMain, true));
        }

        public async Task ReorderAsync(int eventId, List<(int ImageId, int SortOrder)> orders)
        {
            foreach (var (imageId, sortOrder) in orders)
            {
                await _context.EventImages
                    .Where(i => i.ImageId == imageId && i.EventId == eventId)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.SortOrder, sortOrder));
            }
        }
    }
}
