using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface ISeatingTemplateRepository
    {
        Task<List<SeatingTemplate>> GetVisibleToAsync(int organizerId, bool isAdmin = false);

        Task<SeatingTemplate?> GetByIdAsync(int seatingTemplateId);
        Task<SeatingTemplate> CreateAsync(SeatingTemplate entity);
        Task UpdateAsync(SeatingTemplate entity);
        Task SoftDeleteAsync(int seatingTemplateId);
    }
}
