using AuthenticationAPI.DTOs;
using AuthenticationAPI.Repositories;
using System.Text;
using System.Text.RegularExpressions;

namespace AuthenticationAPI.Services
{
    public class ProfileService : IProfileService
    {
        private static readonly Regex ValidFullName =
            new(@"^[\p{L} ]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex ValidPhoneNumber =
            new(@"^0[35789][0-9]{8,13}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly IUserRepository _userRepository;
        private readonly IAvatarStorageService _avatarStorage;

        public ProfileService(IUserRepository userRepository, IAvatarStorageService avatarStorage)
        {
            _userRepository = userRepository;
            _avatarStorage = avatarStorage;
        }

        public async Task<ProfileDTO> UploadAvatarAsync(int userId, IFormFile file)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");
            var upload = await _avatarStorage.UploadAsync(file, user.AvatarPublicId);
            user.AvatarUrl = upload.SecureUrl;
            user.AvatarPublicId = upload.PublicId;
            await _userRepository.SaveChangesAsync();
            return MapToProfileDTO(user);
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
            if (dto == null)
                return (false, "Profile update request is required.", null);

            if (string.IsNullOrWhiteSpace(dto.Email))
                return (false, "Email is required.", null);

            var normalizedFullName = dto.FullName?.Normalize(NormalizationForm.FormC);
            if (string.IsNullOrWhiteSpace(normalizedFullName))
                return (false, "Full name is required.", null);

            var fullName = normalizedFullName.Trim();
            if (normalizedFullName.Length is < 2 or > 100 ||
                fullName.Length is < 2 or > 100 ||
                !ValidFullName.IsMatch(fullName))
                return (false, "Full name must be 2 to 100 letters and spaces only.", null);

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                return (false, "Phone number is required.", null);

            if (!ValidPhoneNumber.IsMatch(dto.PhoneNumber))
                return (false, "Phone number must be 10 to 15 digits and start with 03, 05, 07, 08, or 09.", null);

            var user = await GetCurrentUserAsync(email, userId);

            if (user == null)
            {
                return (
                    false,
                    "User not found.",
                    null
                );
            }

            if (!string.Equals(
                dto.Email.Trim(),
                user.Email,
                StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Email cannot be changed.", null);
            }

            // ==========================================
            // CHECK PHONE
            // ==========================================
            if (dto.PhoneNumber != user.PhoneNumber)
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

            user.FullName = fullName;

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
            // User ID is immutable; email may have changed since the JWT was issued.
            if (int.TryParse(userId, out var id))
            {
                var user = await _userRepository.GetByIdAsync(id);
                if (user != null)
                    return user;
            }

            // Fall back to email for tokens that do not contain a user ID.
            // ------------------------------------------
            if (!string.IsNullOrWhiteSpace(email))
            {
                var user = await _userRepository.GetByEmailAsync(email);

                if (user != null)
                    return user;
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

                IsVerified = user.IsVerified,

                IsActive = user.IsActive,

                EkycStatus = user.EkycStatus,

                CreatedAt = user.CreatedAt,

                RoleName = user.UserRoles
                    .Select(ur => ur.Role.RoleName)
                    .FirstOrDefault(),

                Roles = user.UserRoles
                    .Where(ur => ur.Role != null)
                    .Select(ur => ur.Role.RoleName)
                    .OrderBy(role => role)
                    .ToList()
            };
        }
    }
}
