using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(int userId);
        Task<Role?> GetRoleByNameAsync(string roleName);
        Task<Role?> GetRoleByIdAsync(int roleId);
        Task<bool> UserHasRoleAsync(int userId, string roleName);
        Task<bool> UserHasRoleAsync(int userId, int roleId);
        Task<UserRole?> GetUserRoleAsync(int userId, int roleId);

        Task AddAsync(User user);
        Task AddUserRoleAsync(UserRole userRole);
        void RemoveUserRole(UserRole userRole);

        Task AddRefreshTokenAsync(RefreshToken refreshToken);
        Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash);
        Task RevokeRefreshTokenAsync(string tokenHash);

        Task<List<User>> GetAllAsync();
        Task<User?> GetByPhoneAsync(string phone);
        Task SaveChangesAsync();
    }
}