using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
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
            return users.Select(MapToUserDto).ToList();
        }

        public async Task<UserDTO?> GetUserAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return null;

            return MapToUserDto(user);
        }

        public async Task<(bool Success, string Message)> UpdateUserAsync(int id, UpdateUserDTO dto, int? currentUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return (false, "User not found.");

            if (!string.IsNullOrWhiteSpace(dto.Email) &&
                !dto.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingEmail = await _userRepository.GetByEmailAsync(dto.Email);
                if (existingEmail != null && existingEmail.UserId != id)
                    return (false, "Email already exists.");

                user.Email = dto.Email.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) &&
                dto.PhoneNumber != user.PhoneNumber)
            {
                var existingPhone = await _userRepository.GetByPhoneAsync(dto.PhoneNumber.Trim());
                if (existingPhone != null && existingPhone.UserId != id)
                    return (false, "Phone number already exists.");

                user.PhoneNumber = dto.PhoneNumber.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.AvatarUrl))
                user.AvatarUrl = dto.AvatarUrl.Trim();

            if (dto.RoleIds != null || dto.RoleNames != null)
            {
                var allowedRoleNames = GetAllowedRoleNames();
                var requestedRoleNames = GetRequestedRoleNames(dto, allowedRoleNames);

                if (requestedRoleNames == null)
                    return (false, "Role update is not allowed for this account.");

                if (currentUserId == id && user.UserRoles.Any(ur => ur.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
                    return (false, "You cannot change your own administrator role.");

                if (user.UserRoles.Any(ur => ur.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
                    return (false, "Admin accounts cannot be modified.");

                var normalized = requestedRoleNames
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(name => allowedRoleNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (normalized.Count == 0)
                    return (false, "At least one valid role must be selected.");

                var userRoles = user.UserRoles.ToList();
                foreach (var existing in userRoles)
                {
                    _userRepository.RemoveUserRole(existing);
                }

                foreach (var roleName in normalized)
                {
                    var role = await _userRepository.GetRoleByNameAsync(roleName);
                    if (role == null)
                        return (false, $"Role '{roleName}' not found.");

                    if (await _userRepository.UserHasRoleAsync(user.UserId, role.RoleId))
                        continue;

                    await _userRepository.AddUserRoleAsync(new Models.UserRole
                    {
                        UserId = user.UserId,
                        RoleId = role.RoleId,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }

            await _userRepository.SaveChangesAsync();
            return (true, "");
        }

        public async Task<(bool Success, string Message, bool? IsActive)> UpdateUserStatusAsync(int id, int status, int? currentUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return (false, "User not found.", null);

            if (user.UserRoles.Any(ur => ur.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
                return (false, "Admin accounts cannot be blocked or unblocked.", null);

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
                return (false, "Invalid status. 1 = Unlock, 2 = Block.", null);
            }

            await _userRepository.SaveChangesAsync();
            return (true, user.IsActive ? "User unlocked." : "User blocked.", user.IsActive);
        }

        public async Task<(bool Success, string Message, UserDTO? Data)> AssignRoleAsync(int userId, int roleId, int? currentUserId = null)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return (false, "User not found.", null);

            var currentUserRoles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
            if (currentUserRoles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase)) ||
                currentUserRoles.Any(r => r.Equals("Organizer", StringComparison.OrdinalIgnoreCase)))
            {
                return (false, "Role assignment is not available for Admin or Organizer accounts.", null);
            }

            var role = await _userRepository.GetRoleByIdAsync(roleId);
            if (role == null)
                return (false, "Role not found.", null);

            var validRoleNames = new[] { "Customer", "Staff" };
            if (!validRoleNames.Contains(role.RoleName, StringComparer.OrdinalIgnoreCase))
                return (false, "Only Customer and Staff roles can be assigned.", null);

            if (user.UserRoles.Any(ur => ur.Role.RoleName.Equals(role.RoleName, StringComparison.OrdinalIgnoreCase)))
                return (false, "This user already has this role.", null);

            if (currentUserId == user.UserId && user.UserRoles.Any(ur => ur.Role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
                return (false, "You cannot change your own administrator role.", null);

            var assignment = new Models.UserRole
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
                AssignedAt = DateTime.UtcNow
            };

            await _userRepository.AddUserRoleAsync(assignment);
            await _userRepository.SaveChangesAsync();

            var refreshedUser = await _userRepository.GetByIdAsync(userId);
            return (true, "Role assigned successfully.", refreshedUser == null ? null : MapToUserDto(refreshedUser));
        }

        public async Task<(bool Success, string Message, UserDTO? Data)> AddRoleAsync(int userId, string roleName)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return (false, "User not found.", null);

            var normalizedRoleName = roleName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedRoleName))
                return (false, "RoleName is required.", null);

            if (!normalizedRoleName.Equals("Organizer", StringComparison.OrdinalIgnoreCase))
                return (false, "This workflow may only grant the Organizer role.", null);

            var role = await _userRepository.GetRoleByNameAsync(normalizedRoleName);
            if (role == null)
                return (false, $"Role '{normalizedRoleName}' not found.", null);

            if (await _userRepository.UserHasRoleAsync(userId, role.RoleId))
                return (true, "User already has this role.", MapToUserDto(user));

            await _userRepository.AddUserRoleAsync(new Models.UserRole
            {
                UserId = userId,
                RoleId = role.RoleId,
                AssignedAt = DateTime.UtcNow
            });
            await _userRepository.SaveChangesAsync();

            var refreshedUser = await _userRepository.GetByIdAsync(userId);
            return (true, "Role granted successfully.", refreshedUser == null ? null : MapToUserDto(refreshedUser));
        }

        private static UserDTO MapToUserDto(User user)
        {
            var roles = user.UserRoles
                .Where(ur => ur.Role != null)
                .Select(ur => new RoleDTO
                {
                    RoleId = ur.Role.RoleId,
                    RoleName = ur.Role.RoleName
                })
                .OrderBy(r => r.RoleName)
                .ToList();

            return new UserDTO
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                Roles = roles,
                IsVerified = user.IsVerified,
                EkycStatus = user.EkycStatus,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive
            };
        }

        private static List<string> GetAllowedRoleNames()
        {
            return new List<string> { "Customer", "Staff" };
        }

        private static List<string>? GetRequestedRoleNames(UpdateUserDTO dto, List<string> allowedRoleNames)
        {
            var names = new List<string>();

            if (dto.RoleIds != null)
            {
                foreach (var roleId in dto.RoleIds.Distinct())
                {
                    var role = new RoleDTO { RoleId = roleId };
                    if (role.RoleId > 0)
                    {
                        names.AddRange(allowedRoleNames);
                    }
                }
            }

            if (dto.RoleNames != null)
            {
                foreach (var roleName in dto.RoleNames)
                {
                    if (!string.IsNullOrWhiteSpace(roleName))
                        names.Add(roleName.Trim());
                }
            }

            if (names.Count == 0)
                return new List<string>();

            var invalid = names.FirstOrDefault(name =>
                !string.IsNullOrWhiteSpace(name) &&
                !allowedRoleNames.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                !name.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("Organizer", StringComparison.OrdinalIgnoreCase));

            if (invalid != null)
            {
                return null;
            }

            if (names.Any(name => name.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Organizer", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }
    }
}
