using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly AuthenticationDbContext _context;

        public RoleRepository(AuthenticationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Role>> GetAllAsync()
        {
            return await _context.Roles
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Role?> GetByIdAsync(int id)
        {
            return await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleId == id);
        }

        public async Task<bool> ExistsByNameAsync(
            string roleName,
            int? excludeId = null)
        {
            return await _context.Roles
                .AnyAsync(r =>
                    r.RoleName == roleName &&
                    (!excludeId.HasValue || r.RoleId != excludeId.Value));
        }

        public async Task AddAsync(Role role)
        {
            await _context.Roles.AddAsync(role);
        }

        public void Delete(Role role)
        {
            _context.Roles.Remove(role);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}