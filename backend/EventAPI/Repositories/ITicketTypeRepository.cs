using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface ITicketTypeRepository
    {
        Task<List<TicketType>> GetByEventIdAsync(int eventId);
        Task<TicketType?> GetByIdAsync(int id);
        Task<bool> ExistsNameAsync(int eventId, string typeName, int? excludeId = null);
        Task<TicketType> CreateAsync(TicketType entity);
        Task UpdateAsync(TicketType entity);
        Task SoftDeleteAsync(int id, int deletedBy);
    }
}
