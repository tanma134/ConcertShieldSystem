using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthenticationAPI.Repositories
{
    public interface IKycDeletionRepository
    {
        Task<bool> ExistsForUserAsync(int userId, string status);

        /// <summary>
        /// Inserts and saves the request.
        /// Throws <see cref="DuplicateRecordException"/> if the user already has a pending request
        /// (unique index on user_id WHERE status = 'Pending').
        /// </summary>
        Task CreateAsync(KycDeletionRequest request);

        Task<List<KycDeletionRequest>> GetByUserAsync(int userId);

        Task<(List<KycDeletionRequest> Items, int Total)> GetPageAsync(string? status, int page, int pageSize);

        /// <summary>Tracked, so it can be modified and saved.</summary>
        Task<KycDeletionRequest?> GetByIdAsync(int id);

        Task SaveChangesAsync();

        /// <summary>Starts a transaction shared by every repository in the current request. Dispose without Commit = rollback.</summary>
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
