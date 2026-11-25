using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Repositories
{
    public class EkycRepository : IEkycRepository
    {
        private const string Passed = "Passed";
        private const string ManualReview = "ManualReview";
        private const string Pending = "Pending";

        private readonly AuthenticationDbContext _context;

        public EkycRepository(AuthenticationDbContext context)
        {
            _context = context;
        }

        public async Task<int> CreateAsync(EkycVerification record)
        {
            _context.EkycVerifications.Add(record);
            await _context.SaveChangesAsync();
            return record.EkycId;
        }

        public async Task UpdateAsync(EkycVerification record, string? userEkycStatus = null)
        {
            _context.EkycVerifications.Update(record);

            User? user = null;
            if (userEkycStatus is not null)
            {
                user = await _context.Users.FindAsync(record.UserId);
                if (user is not null && user.EkycStatus != Passed)
                    user.EkycStatus = userEkycStatus;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                DetachUser(user);
                throw new DuplicateRecordException("Unique constraint violated while saving the eKYC record", ex);
            }
            catch
            {
                // Do not leave the unsaved user value in the change tracker, it would be saved by the next SaveChanges
                DetachUser(user);
                throw;
            }
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

        public async Task<EkycVerification?> GetByIdAsync(int ekycId)
        {
            return await _context.EkycVerifications
                .FirstOrDefaultAsync(e => e.EkycId == ekycId);
        }

        public async Task<EkycVerification?> GetActiveByUserAsync(int userId)
        {
            return await _context.EkycVerifications
                .AsNoTracking()
                .Where(e => e.UserId == userId && (e.Status == Passed || e.Status == ManualReview))
                .OrderByDescending(e => e.EkycId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<EkycVerification>> GetRecentByUserAsync(int userId, int take)
        {
            return await _context.EkycVerifications
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.EkycId)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<EkycVerification>> GetAllByUserAsync(int userId)
        {
            return await _context.EkycVerifications
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .ToListAsync();
        }

        public async Task<bool> IsCccdUsedByAnotherUserAsync(string cccdHash, int userId)
        {
            return await _context.EkycVerifications
                .AnyAsync(e => e.CccdNumberHash == cccdHash
                               && e.UserId != userId
                               && (e.Status == Passed || e.Status == ManualReview));
        }

        public async Task<bool> IsCccdVerifiedByAnotherUserAsync(string cccdHash, int userId)
        {
            return await _context.EkycVerifications
                .AnyAsync(e => e.CccdNumberHash == cccdHash
                               && e.UserId != userId
                               && e.Status == Passed);
        }

        public async Task<List<EkycVerification>> GetDeletableByUserAsync(int userId, bool keepDocumentHash)
        {
            return await DeletableQuery(userId, keepDocumentHash).ToListAsync();
        }

        public async Task<bool> HasDeletableAsync(int userId, bool keepDocumentHash)
        {
            return await DeletableQuery(userId, keepDocumentHash).AnyAsync();
        }

        public async Task<(List<EkycVerification> Items, int Total)> GetManualReviewPageAsync(int page, int pageSize)
        {
            var q = _context.EkycVerifications.AsNoTracking().Where(e => e.Status == ManualReview);
            var total = await q.CountAsync();
            var items = await q.OrderBy(e => e.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public async Task<List<EkycVerification>> GetPurgeCandidatesAsync(DateTime cutoffUtc, int batchSize, CancellationToken ct = default)
        {
            return await _context.EkycVerifications
                .Where(e => e.DataPurgedAt == null
                            && !e.LegalHold // data under legal hold never expires
                            && e.Status != Pending && e.Status != ManualReview
                            && (e.VerifiedAt ?? e.CreatedAt) < cutoffUtc)
                .OrderBy(e => e.EkycId)
                .Take(batchSize)
                .ToListAsync(ct);
        }

        /// <summary>Records not purged yet, or purged but still holding the CCCD hash/number that policy says must go.</summary>
        private IQueryable<EkycVerification> DeletableQuery(int userId, bool keepDocumentHash) =>
            _context.EkycVerifications.Where(e => e.UserId == userId
                && (e.DataPurgedAt == null
                    || (!keepDocumentHash && (e.CccdNumberHash != null || e.CccdNumberEncrypted != null))));

        private void DetachUser(User? user)
        {
            if (user is not null)
                _context.Entry(user).State = EntityState.Detached;
        }
    }
}
