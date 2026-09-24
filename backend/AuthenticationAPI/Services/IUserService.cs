using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IUserService
    {
        Task<List<UserDTO>> GetUsersAsync();
        Task<UserDTO?> GetUserAsync(int id);
        Task<(bool Success, string Message)> UpdateUserAsync(int id, UpdateUserDTO dto, int? currentUserId = null);
        Task<(bool Success, string Message, bool? IsActive)> UpdateUserStatusAsync(int id, int status, int? currentUserId = null);
        Task<(bool Success, string Message, UserDTO? Data)> AssignRoleAsync(int userId, int roleId, int? currentUserId = null);
        Task<(bool Success, string Message, UserDTO? Data)> AddRoleAsync(int userId, string roleName);
    }
}
