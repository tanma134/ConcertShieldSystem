
using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Services
{
    /// <summary>
    /// 3.5 - Tự động xóa ảnh gốc + dữ liệu OCR sau N ngày (N lấy từ bảng kyc_settings, 0 = tắt).
    /// Giữ lại: trạng thái, điểm, thời điểm; hash/bản mã hóa số CCCD tùy KeepDocumentHashAfterDeletion.
    /// Không đụng tới hồ sơ đang Pending hoặc ManualReview.
    /// </summary>
    public class KycRetentionService : BackgroundService
    {
        private const int BatchSize = 500;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<KycRetentionService> _logger;

        public KycRetentionService(IServiceScopeFactory scopeFactory, ILogger<KycRetentionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // đợi app khởi động xong

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await PurgeOnceAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "KYC retention job lỗi");
                    }

                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // app đang tắt
            }
        }

        private async Task PurgeOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var settings = await sp.GetRequiredService<IKycSettingService>().GetAsync();

            if (settings.RetentionDays <= 0)
                return;

            var db = sp.GetRequiredService<AuthenticationDbContext>();
            var purger = sp.GetRequiredService<IKycDataPurger>();
            var accessLog = sp.GetRequiredService<IKycAccessLogService>();

            var cutoff = DateTime.UtcNow.AddDays(-settings.RetentionDays);
            var total = 0;

            while (!ct.IsCancellationRequested)
            {
                var candidates = await db.EkycVerifications
                    .Where(e => e.DataPurgedAt == null
                                && e.Status != "Pending" && e.Status != "ManualReview"
                                && (e.VerifiedAt ?? e.CreatedAt) < cutoff)
                    .OrderBy(e => e.EkycId)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (candidates.Count == 0)
                    break;

                var purgedInBatch = 0;
                foreach (var record in candidates)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        await purger.PurgeAsync(record, settings.KeepDocumentHashAfterDeletion, ct);
                        await accessLog.LogAsync(record.UserId, record.EkycId, null, KycActorTypes.System,
                            KycAccessActions.RetentionPurge, null, $"retentionDays={settings.RetentionDays}");
                        purgedInBatch++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Không xóa được dữ liệu KYC của ekyc {EkycId}", record.EkycId);
                    }
                }

                total += purgedInBatch;
                // Có bản ghi lỗi (sẽ xuất hiện lại ở đầu batch) hoặc hết dữ liệu thì dừng, chờ lần chạy sau
                if (purgedInBatch < candidates.Count || candidates.Count < BatchSize)
                    break;
            }

            if (total > 0)
                _logger.LogInformation("KYC retention: đã xóa ảnh/dữ liệu OCR của {Count} hồ sơ (quá {Days} ngày)", total, settings.RetentionDays);
        }
    }
}