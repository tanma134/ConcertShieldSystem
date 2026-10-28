using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface ISeatingTemplateRepository
    {
        /// <summary>Templates an organizer can use: their own plus every public (admin-provided) one.</summary>
        Task<List<SeatingTemplate>> GetVisibleToAsync(int organizerId, bool isAdmin = false);

        Task<SeatingTemplate?> GetByIdAsync(int seatingTemplateId);
        Task<SeatingTemplate> CreateAsync(SeatingTemplate entity);
        Task UpdateAsync(SeatingTemplate entity);
        Task SoftDeleteAsync(int seatingTemplateId);
    }
}
