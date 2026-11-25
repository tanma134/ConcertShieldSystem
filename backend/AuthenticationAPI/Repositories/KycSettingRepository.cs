using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Repositories
{
    public class KycSettingRepository : IKycSettingRepository
    {
        private const int SettingsRowId = 1;

        private readonly AuthenticationDbContext _context;

        public KycSettingRepository(AuthenticationDbContext context)
        {
            _context = context;
        }

        public Task<KycSetting?> GetAsync() =>
            _context.KycSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == SettingsRowId);

        public Task<KycSetting?> GetForUpdateAsync() =>
            _context.KycSettings.FirstOrDefaultAsync(x => x.Id == SettingsRowId);

        public void Add(KycSetting setting) => _context.KycSettings.Add(setting);

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
