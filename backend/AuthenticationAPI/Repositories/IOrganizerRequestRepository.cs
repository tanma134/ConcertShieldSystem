using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    public interface IOrganizerRequestRepository
    {
        Task<OrganizerRequest?> GetByIdAsync(int requestId);

        Task<OrganizerRequest?> GetPendingByUserIdAsync(int userId);

        Task<List<OrganizerRequest>> GetByUserIdAsync(int userId);

        Task<List<OrganizerRequest>> GetAllAsync(string? status);

        Task AddAsync(OrganizerRequest request);

        Task SaveChangesAsync();
    }
}