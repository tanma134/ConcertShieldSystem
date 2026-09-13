using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;

namespace AuthenticationAPI.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepository;

        public RoleService(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<List<object>> GetAllAsync()
        {
            var roles = await _roleRepository.GetAllAsync();

            return roles
                .Select(r => (object)new
                {
                    r.RoleId,
                    r.RoleName
                })
                .ToList();
        }

        public async Task<object?> GetByIdAsync(int id)
        {
            var role = await _roleRepository.GetByIdAsync(id);

            if (role == null)
                return null;

            return new
            {
                role.RoleId,
                role.RoleName
            };
        }

        public async Task<(bool Success, string Message, object? Data)> CreateAsync(
            RoleDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RoleName))
            {
                return (false, "RoleName is required.", null);
            }

            if (await _roleRepository.ExistsByNameAsync(dto.RoleName))
            {
                return (false, "Role already exists.", null);
            }

            var role = new Role
            {
                RoleName = dto.RoleName
            };

            await _roleRepository.AddAsync(role);
            await _roleRepository.SaveChangesAsync();

            var data = new
            {
                role.RoleId,
                role.RoleName
            };

            return (true, "", data);
        }

        public async Task<(bool Success, string Message, object? Data)> UpdateAsync(
            int id,
            RoleDTO dto)
        {
            var role = await _roleRepository.GetByIdAsync(id);

            if (role == null)
            {
                return (false, "Role not found.", null);
            }

            if (string.IsNullOrWhiteSpace(dto.RoleName))
            {
                return (false, "RoleName is required.", null);
            }

            if (await _roleRepository.ExistsByNameAsync(dto.RoleName, id))
            {
                return (
                    false,
                    "Another role with the same name already exists.",
                    null
                );
            }

            role.RoleName = dto.RoleName;

            await _roleRepository.SaveChangesAsync();

            var data = new
            {
                role.RoleId,
                role.RoleName
            };

            return (true, "", data);
        }

        public async Task<(bool Success, string Message)> DeleteAsync(int id)
        {
            var role = await _roleRepository.GetByIdAsync(id);

            if (role == null)
            {
                return (false, "Role not found.");
            }

            _roleRepository.Delete(role);
            await _roleRepository.SaveChangesAsync();

            return (true, "");
        }
    }
}