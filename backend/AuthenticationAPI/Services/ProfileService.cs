using AuthenticationAPI.DTOs;
using AuthenticationAPI.Repositories;

namespace AuthenticationAPI.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IUserRepository _userRepository;

        public ProfileService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // ==========================================
        // GET PROFILE
        // ==========================================
        public async Task<ProfileDTO?> GetProfileAsync(
            string? email,
            string? userId)
        {
            var user = await GetCurrentUserAsync(email, userId);

            if (user == null)
                return null;

            return MapToProfileDTO(user);
        }

        // ==========================================
        // UPDATE PROFILE
        // ==========================================
        public async Task<(bool Success, string Message, ProfileDTO? Profile)>
            UpdateProfileAsync(
                string? email,
                string? userId,
                UpdateProfileDTO dto)
        {
            var user = await GetCurrentUserAsync(email, userId);

            if (user == null)
            {
                return (
                    false,
                    "User not found.",
                    null
                );
            }

            // ==========================================
            // CHECK EMAIL
            // ==========================================
            if (!string.IsNullOrWhiteSpace(dto.Email) &&
                !dto.Email.Equals(
                    user.Email,
                    StringComparison.OrdinalIgnoreCase))
            {
                var existingEmail =
                    await _userRepository.GetByEmailAsync(dto.Email);

                if (existingEmail != null &&
                    existingEmail.UserId != user.UserId)
                {
                    return (
                        false,
                        "Email already exists.",
                        null
                    );
                }

                user.Email = dto.Email.ToLower();
            }

            // ==========================================
            // CHECK PHONE
            // ==========================================
            if (dto.PhoneNumber != null &&
                dto.PhoneNumber != user.PhoneNumber)
            {
                var existingPhone =
                    await _userRepository.GetByPhoneAsync(
                        dto.PhoneNumber);

                if (existingPhone != null &&
                    existingPhone.UserId != user.UserId)
                {
                    return (
                        false,
                        "Phone number already exists.",
                        null
                    );
                }

                user.PhoneNumber = dto.PhoneNumber;
            }

            // ==========================================
            // UPDATE FULL NAME
            // ==========================================
            if (!string.IsNullOrWhiteSpace(dto.FullName))
            {
                user.FullName = dto.FullName;
            }

            // ==========================================
            // UPDATE AVATAR
            // ==========================================
            if (!string.IsNullOrWhiteSpace(dto.AvatarUrl))
            {
                user.AvatarUrl = dto.AvatarUrl;
            }

            await _userRepository.SaveChangesAsync();

            return (
                true,
                "Profile updated successfully.",
                MapToProfileDTO(user)
            );
        }

        // ==========================================
        // GET CURRENT USER
        // ==========================================
        private async Task<AuthenticationAPI.Models.User?> GetCurrentUserAsync(
            string? email,
            string? userId)
        {
            // ------------------------------------------
            // Find by email
            // ------------------------------------------
            if (!string.IsNullOrWhiteSpace(email))
            {
                var user =
                    await _userRepository.GetByEmailAsync(email);

                if (user != null)
                    return user;
            }

            // ------------------------------------------
            // Find by user ID
            // ------------------------------------------
            if (int.TryParse(userId, out var id))
            {
                return await _userRepository.GetByIdAsync(id);
            }

            return null;
        }

        // ==========================================
        // MAP USER -> PROFILE DTO
        // ==========================================
        private ProfileDTO MapToProfileDTO(
            AuthenticationAPI.Models.User user)
        {
            return new ProfileDTO
            {
                UserId = user.UserId,

                Email = user.Email,

                FullName = user.FullName,

                PhoneNumber = user.PhoneNumber,

                AvatarUrl = user.AvatarUrl,

                CreatedAt = user.CreatedAt,

                RoleName = user.UserRoles
                    .Select(ur => ur.Role.RoleName)
                    .FirstOrDefault()
            };
        }
    }
}