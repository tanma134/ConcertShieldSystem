using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Repositories
{
    public class EkycRepository : IEkycRepository
    {
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
                if (user is not null && user.EkycStatus != "Passed")
                    user.EkycStatus = userEkycStatus;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Tránh để giá trị user chưa lưu được còn nằm trong change tracker rồi bị lưu ở lần sau
                if (user is not null)
                    _context.Entry(user).State = EntityState.Detached;
                throw;
            }
        }

        public async Task<EkycVerification?> GetByIdAsync(int ekycId)
        {
            return await _context.EkycVerifications
                .FirstOrDefaultAsync(e => e.EkycId == ekycId);
        }

        public async Task<EkycVerification?> GetActiveByUserAsync(int userId)
        {
            return await _context.EkycVerifications
                .AsNoTracking()
                .Where(e => e.UserId == userId && (e.Status == "Passed" || e.Status == "ManualReview"))
                .OrderByDescending(e => e.EkycId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<EkycVerification>> GetPurgeCandidatesAsync(DateTime cutoffUtc, int take)
        {
            return await _context.EkycVerifications
                .Where(e => e.DataPurgedAt == null
                            && e.Status != "ManualReview"
                            && e.CreatedAt < cutoffUtc)
                .OrderBy(e => e.EkycId)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> IsCccdUsedByAnotherUserAsync(string cccdHash, int userId)
        {
            return await _context.EkycVerifications
                .AnyAsync(e => e.CccdNumberHash == cccdHash
                               && e.UserId != userId
                               && (e.Status == "Passed" || e.Status == "ManualReview"));
        }
    }
}