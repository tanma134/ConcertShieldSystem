using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Repositories
{
    public class OrganizerRequestRepository : IOrganizerRequestRepository
    {
        private readonly AuthenticationDbContext _context;

        public OrganizerRequestRepository(AuthenticationDbContext context)
        {
            _context = context;
        }

        public async Task<OrganizerRequest?> GetByIdAsync(int requestId)
        {
            return await _context.OrganizerRequests
                .Include(r => r.User)
                .Include(r => r.ReviewedByNavigation)
                .Include(r => r.Documents)
                .FirstOrDefaultAsync(r => r.RequestId == requestId && !r.IsDeleted);
        }

        public async Task<OrganizerRequest?> GetPendingByUserIdAsync(int userId)
        {
            return await _context.OrganizerRequests
                .Include(r => r.Documents)
                .Where(r => r.UserId == userId && r.Status == "Pending" && !r.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<List<OrganizerRequest>> GetByUserIdAsync(int userId)
        {
            return await _context.OrganizerRequests
                .Include(r => r.User)
                .Include(r => r.ReviewedByNavigation)
                .Include(r => r.Documents)
                .Where(r => r.UserId == userId && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<OrganizerRequest>> GetAllAsync(string? status)
        {
            var query = _context.OrganizerRequests
                .Include(r => r.User)
                .Include(r => r.ReviewedByNavigation)
                .Include(r => r.Documents)
                .Where(r => !r.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(OrganizerRequest request)
        {
            await _context.OrganizerRequests.AddAsync(request);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
