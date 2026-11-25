using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;

namespace AuthenticationAPI.Services
{
    public class KycSettingService : IKycSettingService
    {
        // Retention must not be shorter than the legal minimum nor longer than necessary.
        // Set the real legal minimum in appsettings (Kyc:LegalMinRetentionDays); these are only the defaults.
        private const int DefaultLegalMinRetentionDays = 30;
        private const int DefaultMaxRetentionDays = 365;

        private readonly IKycSettingRepository _repository;
        private readonly ILogger<KycSettingService> _logger;
        private readonly int _minDays;
        private readonly int _maxDays;

        public KycSettingService(IKycSettingRepository repository, ILogger<KycSettingService> logger, IConfiguration config)
        {
            _repository = repository;
            _logger = logger;
            _minDays = Math.Max(1, config.GetValue("Kyc:LegalMinRetentionDays", DefaultLegalMinRetentionDays));
            _maxDays = Math.Max(_minDays, config.GetValue("Kyc:MaxRetentionDays", DefaultMaxRetentionDays));
        }

        public async Task<KycSettingDto> GetAsync()
        {
            var s = await _repository.GetAsync();
            // No settings row yet: use the defaults (30 days, keep the CCCD hash)
            return s is null
                ? new KycSettingDto
                {
                    RetentionDays = Math.Clamp(DefaultLegalMinRetentionDays, _minDays, _maxDays),
                    KeepDocumentHashAfterDeletion = true,
                    MinRetentionDays = _minDays,
                    MaxRetentionDays = _maxDays
                }
                : Map(s);
        }

        public async Task<KycSettingDto> UpdateAsync(UpdateKycSettingDto dto, int? adminId)
        {
            // never below the legal minimum, never "keep forever"
            if (dto.RetentionDays < _minDays || dto.RetentionDays > _maxDays)
                throw new InvalidOperationException($"Retention days must be between {_minDays} (legal minimum) and {_maxDays}");

            var s = await _repository.GetForUpdateAsync();
            if (s is null)
            {
                s = new KycSetting { Id = 1 };
                _repository.Add(s);
            }

            var oldDays = s.RetentionDays;
            var oldKeep = s.KeepDocumentHashAfterDeletion;

            s.RetentionDays = dto.RetentionDays;
            s.KeepDocumentHashAfterDeletion = dto.KeepDocumentHashAfterDeletion;
            s.UpdatedAt = DateTime.UtcNow;
            s.UpdatedBy = adminId;
            await _repository.SaveChangesAsync();

            _logger.LogWarning("KYC settings changed by admin {AdminId}: retentionDays {OldDays}->{NewDays}, keepHash {OldKeep}->{NewKeep}",
                adminId, oldDays, s.RetentionDays, oldKeep, s.KeepDocumentHashAfterDeletion);

            return Map(s);
        }

        // A value already stored outside the allowed range (e.g. the old 0 = never delete) is clamped,
        // so the retention job and the screens always work with a lawful period.
        private KycSettingDto Map(KycSetting s) => new()
        {
            RetentionDays = Math.Clamp(s.RetentionDays <= 0 ? _maxDays : s.RetentionDays, _minDays, _maxDays),
            KeepDocumentHashAfterDeletion = s.KeepDocumentHashAfterDeletion,
            MinRetentionDays = _minDays,
            MaxRetentionDays = _maxDays,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy
        };
    }
}
