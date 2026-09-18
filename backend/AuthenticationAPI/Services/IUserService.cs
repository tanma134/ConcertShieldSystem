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

        /// <summary>
        /// Grants a role ADDITIVELY - existing roles are kept. Used when EventAPI
        /// approves a concert and the owner must gain "Organizer" while staying "Customer".
        /// Idempotent: granting a role the user already has is a no-op success.
        /// </summary>
        Task<(bool Success, string Message, List<string> Roles)> AddRoleAsync(
            int userId,
            string roleName);
    }
}