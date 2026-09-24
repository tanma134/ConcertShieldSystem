using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories
{
    public class EventRepository : IEventRepository
    {
        private readonly EventDbContext _context;

        public EventRepository(EventDbContext context)
        {
            _context = context;
        }

        public async Task<Event?> GetByIdAsync(int id, bool includeChildren = false)
        {
            var query = _context.Events.AsQueryable();

            if (includeChildren)
            {
                query = query
                    .Include(e => e.TicketTypes.Where(t => !t.IsDeleted).OrderBy(t => t.SortOrder))
                    .Include(e => e.EventImages.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
                    .Include(e => e.RefundPolicies.Where(r => r.IsActive));
            }

            return await query.FirstOrDefaultAsync(e => e.EventId == id && !e.IsDeleted);
        }

        public async Task<Event?> GetBySlugAsync(string slug, bool includeChildren = false)
        {
            var query = _context.Events.AsQueryable();

            if (includeChildren)
            {
                query = query
                    .Include(e => e.TicketTypes.Where(t => !t.IsDeleted).OrderBy(t => t.SortOrder))
                    .Include(e => e.EventImages.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
                    .Include(e => e.RefundPolicies.Where(r => r.IsActive));
            }

            return await query.FirstOrDefaultAsync(e => e.Slug == slug && !e.IsDeleted);
        }

        public async Task<int> CountTicketTypesAsync(int eventId)
        {
            return await _context.TicketTypes
                .CountAsync(t => t.EventId == eventId && !t.IsDeleted);
        }

        public async Task<int> CountRefundPoliciesAsync(int eventId)
        {
            return await _context.RefundPolicies
                .CountAsync(r => r.EventId == eventId && r.IsActive);
        }

        public async Task<(List<Event> Items, int TotalCount)> GetFilteredAsync(EventFilterDTO filter)
        {
            var query = _context.Events
                .Include(e => e.TicketTypes.Where(t => !t.IsDeleted))
                .Where(e => !e.IsDeleted)
                .AsQueryable();

            // Search by title
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(e =>
                    e.Title.ToLower().Contains(search) ||
                    (e.ShortDescription != null && e.ShortDescription.ToLower().Contains(search)) ||
                    (e.LocationName != null && e.LocationName.ToLower().Contains(search)));
            }

            // Filter by city
            if (!string.IsNullOrWhiteSpace(filter.City))
                query = query.Where(e => e.City != null && e.City.ToLower() == filter.City.ToLower());

            // Visibility. PublishedOnly is set by the controller for anonymous/public
            // endpoints and always wins over the caller-supplied Status parameter, so a
            // public request can never enumerate Draft/Pending/Rejected concerts.
            if (filter.PublishedOnly)
            {
                query = query.Where(e =>
                    e.Status == EventStatus.Published || e.Status == EventStatus.LegacyApproved);
            }
            else if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = EventStatus.Normalize(filter.Status);
                query = status == EventStatus.Published
                    ? query.Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.LegacyApproved)
                    : query.Where(e => e.Status == status);
            }

            // Filter by date range
            if (filter.DateFrom.HasValue)
                query = query.Where(e => e.StartsAt >= filter.DateFrom.Value);

            if (filter.DateTo.HasValue)
                query = query.Where(e => e.EndsAt <= filter.DateTo.Value);

            // Filter by price range (requires TicketTypes join)
            if (filter.MinPrice.HasValue)
                query = query.Where(e => e.TicketTypes.Any(t => !t.IsDeleted && t.Price >= filter.MinPrice.Value));

            if (filter.MaxPrice.HasValue)
                query = query.Where(e => e.TicketTypes.Any(t => !t.IsDeleted && t.Price <= filter.MaxPrice.Value));

            // Filter by organizer
            if (filter.OrganizerId.HasValue)
                query = query.Where(e => e.OrganizerId == filter.OrganizerId.Value);

            // Filter by featured
            if (filter.IsFeatured.HasValue)
                query = query.Where(e => e.IsFeatured == filter.IsFeatured.Value);

            // Get total count
            int totalCount = await query.CountAsync();

            // Sorting
            query = filter.SortBy?.ToLower() switch
            {
                "title" => filter.SortOrder == "asc" ? query.OrderBy(e => e.Title) : query.OrderByDescending(e => e.Title),
                "startsat" => filter.SortOrder == "asc" ? query.OrderBy(e => e.StartsAt) : query.OrderByDescending(e => e.StartsAt),
                "viewcount" => filter.SortOrder == "asc" ? query.OrderBy(e => e.ViewCount) : query.OrderByDescending(e => e.ViewCount),
                "soldtickets" => filter.SortOrder == "asc" ? query.OrderBy(e => e.SoldTickets) : query.OrderByDescending(e => e.SoldTickets),
                _ => filter.SortOrder == "asc" ? query.OrderBy(e => e.CreatedAt) : query.OrderByDescending(e => e.CreatedAt),
            };

            // Pagination
            var items = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .AsNoTracking()
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<Event>> GetFeaturedAsync(int count = 10)
        {
            return await _context.Events
                .Include(e => e.TicketTypes.Where(t => !t.IsDeleted))
                .Where(e => !e.IsDeleted && e.IsFeatured &&
                            (e.Status == EventStatus.Published || e.Status == EventStatus.LegacyApproved))
                .OrderBy(e => e.StartsAt)
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Event>> GetByOrganizerIdAsync(int organizerId)
        {
            return await _context.Events
                .Include(e => e.TicketTypes.Where(t => !t.IsDeleted))
                .Where(e => e.OrganizerId == organizerId && !e.IsDeleted)
                .OrderByDescending(e => e.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<(List<Event> Items, int TotalCount)> GetDeletedAsync(int page = 1, int pageSize = 12)
        {
            var query = _context.Events.Where(e => e.IsDeleted);
            int totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(e => e.DeletedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(List<Event> Items, int TotalCount)> GetModerationQueueAsync(int page = 1, int pageSize = 12)
        {
            var query = _context.Events
                .Include(e => e.TicketTypes.Where(t => !t.IsDeleted))
                .Where(e => !e.IsDeleted && e.Status == EventStatus.Pending);

            int totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(e => e.SubmittedAt ?? e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Event> CreateAsync(Event entity)
        {
            _context.Events.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(Event entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(int id, int deletedBy)
        {
            var entity = await _context.Events.FindAsync(id);
            if (entity != null)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.DeletedBy = deletedBy;
                await _context.SaveChangesAsync();
            }
        }

        public async Task HardDeleteAsync(int id)
        {
            var entity = await _context.Events
                .Include(e => e.TicketTypes)
                .Include(e => e.EventImages)
                .Include(e => e.RefundPolicies)
                .Include(e => e.SeatMaps).ThenInclude(s => s.SeatZones).ThenInclude(z => z.Seats)
                .Include(e => e.Wishlists)
                .Include(e => e.Reviews)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (entity != null)
            {
                _context.Events.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public async Task RestoreAsync(int id)
        {
            var entity = await _context.Events.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.EventId == id);
            if (entity != null)
            {
                entity.IsDeleted = false;
                entity.DeletedAt = null;
                entity.DeletedBy = null;
                await _context.SaveChangesAsync();
            }
        }

        public async Task IncrementViewCountAsync(int id)
        {
            await _context.Events
                .Where(e => e.EventId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ViewCount, e => e.ViewCount + 1));
        }
    }
}
