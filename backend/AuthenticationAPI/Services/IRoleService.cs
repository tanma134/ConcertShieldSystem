using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IRoleService
    {
        Task<List<object>> GetAllAsync();
        Task<object?> GetByIdAsync(int id);
        Task<(bool Success, string Message, object? Data)> CreateAsync(RoleDTO dto);
        Task<(bool Success, string Message, object? Data)> UpdateAsync(int id, RoleDTO dto);
        Task<(bool Success, string Message)> DeleteAsync(int id);
    }
}