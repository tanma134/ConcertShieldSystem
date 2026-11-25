using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    public interface IEkycRepository
    {
        Task<int> CreateAsync(EkycVerification record);

        /// <summary>
        /// Saves the record. When userEkycStatus is given, User.EkycStatus is updated in the same
        /// SaveChanges (a user already "Passed" is never downgraded).
        /// Throws <see cref="DuplicateRecordException"/> when a unique index (e.g. one CCCD per account) is violated.
        /// </summary>
        Task UpdateAsync(EkycVerification record, string? userEkycStatus = null);

        /// <summary>Persists changes made to records that were loaded by this repository (tracked).</summary>
        Task SaveChangesAsync(CancellationToken ct = default);

        Task<EkycVerification?> GetByIdAsync(int ekycId);

        /// <summary>Latest record of the user with status Passed or ManualReview (null if none).</summary>
        Task<EkycVerification?> GetActiveByUserAsync(int userId);

        /// <summary>The user's N most recent records (newest first), used to count consecutive failures.</summary>
        Task<List<EkycVerification>> GetRecentByUserAsync(int userId, int take);

        /// <summary>All records of a user, read-only.</summary>
        Task<List<EkycVerification>> GetAllByUserAsync(int userId);

        /// <summary>Has this CCCD (by hash) been verified / is it awaiting review on ANOTHER account?</summary>
        Task<bool> IsCccdUsedByAnotherUserAsync(string cccdHash, int userId);

        /// <summary>Has this CCCD (by hash) been verified (Passed) on ANOTHER account?</summary>
        Task<bool> IsCccdVerifiedByAnotherUserAsync(string cccdHash, int userId);

        /// <summary>Records that still hold data to delete (tracked, so they can be modified and saved).</summary>
        Task<List<EkycVerification>> GetDeletableByUserAsync(int userId, bool keepDocumentHash);

        Task<bool> HasDeletableAsync(int userId, bool keepDocumentHash);

        /// <summary>Requests waiting for manual review, oldest first.</summary>
        Task<(List<EkycVerification> Items, int Total)> GetManualReviewPageAsync(int page, int pageSize);

        /// <summary>Records whose data has passed the retention period (tracked).</summary>
        Task<List<EkycVerification>> GetPurgeCandidatesAsync(DateTime cutoffUtc, int batchSize, CancellationToken ct = default);
    }
}
