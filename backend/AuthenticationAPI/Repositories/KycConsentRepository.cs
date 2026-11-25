using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthenticationAPI.Repositories
{
    public class KycConsentRepository : IKycConsentRepository
    {
        private readonly AuthenticationDbContext _context;

        public KycConsentRepository(AuthenticationDbContext context)
        {
            _context = context;
        }

        public Task<KycConsentVersion?> GetActiveAsync() =>
            _context.KycConsentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive);

        public Task<string?> GetActiveVersionAsync() =>
            _context.KycConsentVersions.AsNoTracking()
                .Where(x => x.IsActive).Select(x => x.Version).FirstOrDefaultAsync();

        public Task<List<KycConsentVersion>> GetAllAsync() =>
            _context.KycConsentVersions.AsNoTracking()
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .ToListAsync();

        public Task<KycConsentVersion?> GetByIdAsync(int id) =>
            _context.KycConsentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

        public Task<KycConsentVersion?> GetByIdForUpdateAsync(int id) =>
            _context.KycConsentVersions.FirstOrDefaultAsync(x => x.Id == id);

        public Task<bool> VersionExistsAsync(string version) =>
            _context.KycConsentVersions.AnyAsync(x => x.Version == version);

        public Task<Dictionary<string, int>> GetConsentCountsAsync() =>
            _context.EkycVerifications.AsNoTracking()
                .Where(e => e.ConsentVersion != null)
                .GroupBy(e => e.ConsentVersion!)
                .Select(g => new { Version = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Version, x => x.Count);

        public Task<int> CountConsentsAsync(string version) =>
            _context.EkycVerifications.CountAsync(e => e.ConsentVersion == version);

        public Task<bool> HasConsentsAsync(string version) =>
            _context.EkycVerifications.AnyAsync(e => e.ConsentVersion == version);

        public async Task AddAsync(KycConsentVersion version)
        {
            _context.KycConsentVersions.Add(version);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(KycConsentVersion version)
        {
            _context.KycConsentVersions.Remove(version);
            await _context.SaveChangesAsync();
        }

        public Task DeactivateAllAsync() =>
            _context.KycConsentVersions.Where(x => x.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public Task<IDbContextTransaction> BeginTransactionAsync() => _context.Database.BeginTransactionAsync();
    }
}
