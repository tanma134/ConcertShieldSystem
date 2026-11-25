using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthenticationAPI.Repositories
{
    public interface IKycConsentRepository
    {
        Task<KycConsentVersion?> GetActiveAsync();
        Task<string?> GetActiveVersionAsync();
        Task<List<KycConsentVersion>> GetAllAsync();

        Task<KycConsentVersion?> GetByIdAsync(int id);

        Task<KycConsentVersion?> GetByIdForUpdateAsync(int id);

        Task<bool> VersionExistsAsync(string version);

        Task<Dictionary<string, int>> GetConsentCountsAsync();
        Task<int> CountConsentsAsync(string version);
        Task<bool> HasConsentsAsync(string version);

        Task AddAsync(KycConsentVersion version);
        Task DeleteAsync(KycConsentVersion version);

 
        Task DeactivateAllAsync();

        Task SaveChangesAsync();

 
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
