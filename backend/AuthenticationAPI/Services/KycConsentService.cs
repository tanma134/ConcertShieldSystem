using System.Text.Json;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Services
{
    public class KycConsentService : IKycConsentService
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly AuthenticationDbContext _db;
        private readonly IKycSettingService _settings;
        private readonly IConfiguration _config;

        public KycConsentService(AuthenticationDbContext db, IKycSettingService settings, IConfiguration config)
        {
            _db = db;
            _settings = settings;
            _config = config;
        }

        public async Task<KycConsentPublicDto> GetActiveAsync()
        {
            var active = await _db.KycConsentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive)
                ?? throw new InvalidOperationException("No active eKYC consent version is configured");

            var days = (await _settings.GetAsync()).RetentionDays.ToString();
            var contact = _config["Kyc:PrivacyContact"] ?? "";

            string Fill(string text) => text
                .Replace("{retentionDays}", days)
                .Replace("{privacyContact}", contact);

            return new KycConsentPublicDto
            {
                Version = active.Version,
                Title = active.Title,
                CheckboxText = active.CheckboxText,
                Items = ParseItems(active.ContentJson)
                    .Select(i => new ConsentItemDto { Heading = i.Heading, Content = Fill(i.Content) })
                    .ToList()
            };
        }

        public async Task<string> GetActiveVersionAsync()
        {
            return await _db.KycConsentVersions.AsNoTracking()
                       .Where(x => x.IsActive).Select(x => x.Version).FirstOrDefaultAsync()
                   ?? throw new InvalidOperationException("No active eKYC consent version is configured");
        }

        public async Task<List<KycConsentVersionDto>> ListAsync()
        {
            var counts = await _db.EkycVerifications.AsNoTracking()
                .Where(e => e.ConsentVersion != null)
                .GroupBy(e => e.ConsentVersion!)
                .Select(g => new { Version = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Version, x => x.Count);

            var rows = await _db.KycConsentVersions.AsNoTracking()
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .ToListAsync();

            return rows.Select(r => Map(r, counts.GetValueOrDefault(r.Version))).ToList();
        }

        public async Task<KycConsentVersionDto> GetAsync(int id)
        {
            var row = await _db.KycConsentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new KeyNotFoundException("Consent version not found");
            var count = await _db.EkycVerifications.CountAsync(e => e.ConsentVersion == row.Version);
            return Map(row, count);
        }

        public async Task<KycConsentVersionDto> CreateAsync(CreateKycConsentVersionDto dto, int? adminId)
        {
            var version = (dto.Version ?? "").Trim();
            var title = (dto.Title ?? "").Trim();
            var checkbox = (dto.CheckboxText ?? "").Trim();

            if (version.Length == 0 || version.Length > 20)
                throw new InvalidOperationException("Version is required (max 20 characters)");
            if (title.Length == 0 || title.Length > 500)
                throw new InvalidOperationException("Title is required (max 500 characters)");
            if (checkbox.Length == 0)
                throw new InvalidOperationException("Checkbox text is required");
            if (dto.Items is null || dto.Items.Count == 0)
                throw new InvalidOperationException("At least one content section is required");
            if (dto.Items.Any(i => string.IsNullOrWhiteSpace(i.Heading) || string.IsNullOrWhiteSpace(i.Content)))
                throw new InvalidOperationException("Every section needs both a heading and content");

            if (await _db.KycConsentVersions.AnyAsync(x => x.Version == version))
                throw new InvalidOperationException($"Version '{version}' already exists");

            var items = dto.Items.Select(i => new ConsentItemDto
            {
                Heading = i.Heading.Trim(),
                Content = i.Content.Trim()
            });

            var entity = new KycConsentVersion
            {
                Version = version,
                Title = title,
                ContentJson = JsonSerializer.Serialize(items),
                CheckboxText = checkbox,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = adminId
            };
            _db.KycConsentVersions.Add(entity);
            await _db.SaveChangesAsync();

            if (dto.Activate)
                await ActivateAsync(entity.Id);

            return await GetAsync(entity.Id);
        }

        public async Task ActivateAsync(int id)
        {
            var entity = await _db.KycConsentVersions.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new KeyNotFoundException("Consent version not found");
            if (entity.IsActive)
                return;

            await using var tx = await _db.Database.BeginTransactionAsync();
            // Tắt bản cũ trước, rồi bật bản mới (index unique một-bản-active ở DB không bị vi phạm)
            await _db.KycConsentVersions.Where(x => x.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
            entity.IsActive = true;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.KycConsentVersions.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new KeyNotFoundException("Consent version not found");

            if (entity.IsActive)
                throw new InvalidOperationException("Cannot delete the active version");
            if (await _db.EkycVerifications.AnyAsync(e => e.ConsentVersion == entity.Version))
                throw new InvalidOperationException("Cannot delete a version that users have already consented to");

            _db.KycConsentVersions.Remove(entity);
            await _db.SaveChangesAsync();
        }

        private static List<ConsentItemDto> ParseItems(string json)
        {
            try { return JsonSerializer.Deserialize<List<ConsentItemDto>>(json, ReadOptions) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private static KycConsentVersionDto Map(KycConsentVersion v, int consentCount) => new()
        {
            Id = v.Id,
            Version = v.Version,
            Title = v.Title,
            Items = ParseItems(v.ContentJson),
            CheckboxText = v.CheckboxText,
            IsActive = v.IsActive,
            CreatedAt = v.CreatedAt,
            CreatedBy = v.CreatedBy,
            ConsentCount = consentCount
        };
    }
}