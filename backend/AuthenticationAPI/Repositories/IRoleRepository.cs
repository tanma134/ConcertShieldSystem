using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    public interface IRoleRepository
    {
        Task<List<Role>> GetAllAsync();
        Task<Role?> GetByIdAsync(int id);
        Task<bool> ExistsByNameAsync(string roleName, int? excludeId = null);
        Task AddAsync(Role role);
        void Delete(Role role);
        Task SaveChangesAsync();
    }
}