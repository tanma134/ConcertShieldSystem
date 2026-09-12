using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(int userId);
        Task<Role?> GetRoleByNameAsync(string roleName);

        Task AddAsync(User user);
        Task AddUserRoleAsync(UserRole userRole);

        Task AddRefreshTokenAsync(RefreshToken refreshToken);
        Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash);
        Task RevokeRefreshTokenAsync(string tokenHash);

        Task<List<User>> GetAllAsync();
        Task<User?> GetByPhoneAsync(string phone);
        Task SaveChangesAsync();
    }
}