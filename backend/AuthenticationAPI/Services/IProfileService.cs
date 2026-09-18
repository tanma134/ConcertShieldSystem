using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IProfileService
    {
        Task<ProfileDTO?> GetProfileAsync(
            string? email,
            string? userId);

        Task<(bool Success, string Message, ProfileDTO? Profile)>
            UpdateProfileAsync(
                string? email,
                string? userId,
                UpdateProfileDTO dto);

        Task<ProfileDTO> UploadAvatarAsync(int userId, IFormFile file);
    }
}