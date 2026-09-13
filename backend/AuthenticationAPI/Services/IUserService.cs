using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IUserService
    {
        Task<List<UserDTO>> GetUsersAsync();
        Task<UserDTO?> GetUserAsync(int id);
        Task<(bool Success, string Message)> UpdateUserAsync(int id, UserDTO dto);
        Task<(bool Success, string Message, bool? IsActive)> UpdateUserStatusAsync(
            int id,
            int status);
    }
}