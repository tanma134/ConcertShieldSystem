using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthenticationAPI.Repositories
{
    public class KycDeletionRepository : IKycDeletionRepository
    {
        private readonly AuthenticationDbContext _context;

        public KycDeletionRepository(AuthenticationDbContext context)
        {
            _context = context;
        }

        public Task<bool> ExistsForUserAsync(int userId, string status) =>
            _context.KycDeletionRequests.AnyAsync(r => r.UserId == userId && r.Status == status);

        public async Task CreateAsync(KycDeletionRequest request)
        {
            _context.KycDeletionRequests.Add(request);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                _context.Entry(request).State = EntityState.Detached;
                throw new DuplicateRecordException("A pending deletion request already exists for this user", ex);
            }
        }

        public Task<List<KycDeletionRequest>> GetByUserAsync(int userId) =>
            _context.KycDeletionRequests.AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

        public async Task<(List<KycDeletionRequest> Items, int Total)> GetPageAsync(string? status, int page, int pageSize)
        {
            var q = _context.KycDeletionRequests.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(status))
                q = q.Where(r => r.Status == status);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.RequestedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public Task<KycDeletionRequest?> GetByIdAsync(int id) =>
            _context.KycDeletionRequests.FirstOrDefaultAsync(r => r.Id == id);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public Task<IDbContextTransaction> BeginTransactionAsync() => _context.Database.BeginTransactionAsync();
    }
}
