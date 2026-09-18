using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface ITicketTypeRepository
    {
        Task<List<TicketType>> GetByEventIdAsync(int eventId);
        Task<TicketType?> GetByIdAsync(int id);
        Task<TicketType> CreateAsync(TicketType entity);
        Task UpdateAsync(TicketType entity);
        Task SoftDeleteAsync(int id, int deletedBy);
    }
}
