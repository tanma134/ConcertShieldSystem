using AuthenticationAPI.DTOs;
using AuthenticationAPI.Repositories;

namespace AuthenticationAPI.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<List<UserDTO>> GetUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();

            return users.Select(u => new UserDTO
            {
                UserId = u.UserId,
                Email = u.Email,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber,
                AvatarUrl = u.AvatarUrl,
                RoleName = u.UserRoles
                    .Select(ur => ur.Role.RoleName)
                    .FirstOrDefault(),
                CreatedAt = u.CreatedAt,
                IsActive = u.IsActive
            }).ToList();
        }

        public async Task<UserDTO?> GetUserAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
                return null;

            return new UserDTO
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                RoleName = user.UserRoles
                    .Select(ur => ur.Role.RoleName)
                    .FirstOrDefault(),
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive
            };
        }

        public async Task<(bool Success, string Message)> UpdateUserAsync(
            int id,
            UserDTO dto)
        {
            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
                return (false, "User not found.");

            // Check email duplicate
            if (!string.IsNullOrWhiteSpace(dto.Email) &&
                dto.Email.ToLower() != user.Email.ToLower())
            {
                var existingEmail =
                    await _userRepository.GetByEmailAsync(dto.Email);

                if (existingEmail != null &&
                    existingEmail.UserId != id)
                {
                    return (false, "Email already exists.");
                }

                user.Email = dto.Email.ToLower();
            }

            // Check phone duplicate
            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) &&
                dto.PhoneNumber != user.PhoneNumber)
            {
                var existingPhone =
                    await _userRepository.GetByPhoneAsync(dto.PhoneNumber);

                if (existingPhone != null &&
                    existingPhone.UserId != id)
                {
                    return (false, "Phone number already exists.");
                }

                user.PhoneNumber = dto.PhoneNumber;
            }

            // Update information
            user.FullName = dto.FullName;
            user.AvatarUrl = dto.AvatarUrl;

            // Update role
            if (!string.IsNullOrWhiteSpace(dto.RoleName))
            {
                var role =
                    await _userRepository.GetRoleByNameAsync(dto.RoleName);

                if (role == null)
                    return (false, "Role not found.");

                user.UserRoles.Clear();

                user.UserRoles.Add(new Models.UserRole
                {
                    UserId = user.UserId,
                    RoleId = role.RoleId
                });
            }

            await _userRepository.SaveChangesAsync();

            return (true, "");
        }

        public async Task<(bool Success, string Message, bool? IsActive)>
            UpdateUserStatusAsync(int id, int status)
        {
            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
                return (false, "User not found.", null);

            if (status == 1)
            {
                user.IsActive = true;
            }
            else if (status == 2)
            {
                user.IsActive = false;
            }
            else
            {
                return (
                    false,
                    "Invalid status. 1 = Unlock, 2 = Block.",
                    null
                );
            }

            await _userRepository.SaveChangesAsync();

            return (
                true,
                user.IsActive
                    ? "User unlocked."
                    : "User blocked.",
                user.IsActive
            );
        }
    }
}