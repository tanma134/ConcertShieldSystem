using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    public interface IKycSettingRepository
    {
        /// <summary>The single settings row (Id = 1), read-only. Null if it does not exist yet.</summary>
        Task<KycSetting?> GetAsync();

        /// <summary>Same row, tracked, so it can be modified and saved.</summary>
        Task<KycSetting?> GetForUpdateAsync();

        void Add(KycSetting setting);

        Task SaveChangesAsync();
    }
}
