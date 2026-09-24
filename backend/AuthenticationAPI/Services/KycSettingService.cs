
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Services
{


    public class KycSettingService : IKycSettingService
    {
        public const int MaxRetentionDays = 3650;

        private readonly AuthenticationDbContext _db;
        private readonly ILogger<KycSettingService> _logger;

        public KycSettingService(AuthenticationDbContext db, ILogger<KycSettingService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<KycSettingDto> GetAsync()
        {
            var s = await _db.KycSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
            // Chưa có dòng cấu hình: dùng mặc định (30 ngày, giữ hash CCCD)
            return s is null
                ? new KycSettingDto { RetentionDays = 30, KeepDocumentHashAfterDeletion = true }
                : Map(s);
        }

        public async Task<KycSettingDto> UpdateAsync(UpdateKycSettingDto dto, int? adminId)
        {
            if (dto.RetentionDays < 0 || dto.RetentionDays > MaxRetentionDays)
                throw new InvalidOperationException($"Retention days must be between 0 and {MaxRetentionDays} (0 = never auto-delete)");

            var s = await _db.KycSettings.FirstOrDefaultAsync(x => x.Id == 1);
            if (s is null)
            {
                s = new KycSetting { Id = 1 };
                _db.KycSettings.Add(s);
            }

            var oldDays = s.RetentionDays;
            var oldKeep = s.KeepDocumentHashAfterDeletion;

            s.RetentionDays = dto.RetentionDays;
            s.KeepDocumentHashAfterDeletion = dto.KeepDocumentHashAfterDeletion;
            s.UpdatedAt = DateTime.UtcNow;
            s.UpdatedBy = adminId;
            await _db.SaveChangesAsync();

            _logger.LogWarning("KYC settings changed by admin {AdminId}: retentionDays {OldDays}->{NewDays}, keepHash {OldKeep}->{NewKeep}",
                adminId, oldDays, s.RetentionDays, oldKeep, s.KeepDocumentHashAfterDeletion);

            return Map(s);
        }

        private static KycSettingDto Map(KycSetting s) => new()
        {
            RetentionDays = s.RetentionDays,
            KeepDocumentHashAfterDeletion = s.KeepDocumentHashAfterDeletion,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy
        };
    }
}