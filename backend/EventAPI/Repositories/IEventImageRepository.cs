using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface IEventImageRepository
    {
        Task<List<EventImage>> GetByEventIdAsync(int eventId);
        Task<EventImage?> GetByIdAsync(int id);
        Task<EventImage> CreateAsync(EventImage entity);
        Task UpdateAsync(EventImage entity);
        Task SoftDeleteAsync(int id, int deletedBy);
        Task SetMainAsync(int eventId, int imageId);
        Task ReorderAsync(int eventId, List<(int ImageId, int SortOrder)> orders);
    }
}
