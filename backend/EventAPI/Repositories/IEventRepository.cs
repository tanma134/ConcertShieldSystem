using EventAPI.DTOs;
using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface IEventRepository
    {
        Task<Event?> GetByIdAsync(int id, bool includeChildren = false);
        Task<Event?> GetBySlugAsync(string slug, bool includeChildren = false);

        // Counts non-deleted ticket types for an event (cheap pre-submit check).
        Task<int> CountTicketTypesAsync(int eventId);

        // Counts active refund policies for an event.
        Task<int> CountRefundPoliciesAsync(int eventId);
        Task<(List<Event> Items, int TotalCount)> GetFilteredAsync(EventFilterDTO filter);
        Task<List<Event>> GetFeaturedAsync(int count = 10);
        Task<List<Event>> GetByOrganizerIdAsync(int organizerId);
        Task<(List<Event> Items, int TotalCount)> GetDeletedAsync(int page = 1, int pageSize = 12);
        Task<(List<Event> Items, int TotalCount)> GetModerationQueueAsync(int page = 1, int pageSize = 12);
        Task<Event> CreateAsync(Event entity);
        Task UpdateAsync(Event entity);
        Task SoftDeleteAsync(int id, int deletedBy);
        Task HardDeleteAsync(int id);
        Task RestoreAsync(int id);
        Task IncrementViewCountAsync(int id);
    }
}
