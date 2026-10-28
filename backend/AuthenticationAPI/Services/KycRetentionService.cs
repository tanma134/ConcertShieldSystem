
using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Services
{
    /// <summary>
    /// 3.5 - T? ð?ng xóa ?nh g?c + d? li?u OCR sau N ngày (N l?y t? b?ng kyc_settings, 0 = t?t).
    /// Gi? l?i: tr?ng thái, ði?m, th?i ði?m; hash/b?n m? hóa s? CCCD tùy KeepDocumentHashAfterDeletion.
    /// Không ð?ng t?i h? sõ ðang Pending ho?c ManualReview.
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
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // ð?i app kh?i ð?ng xong

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await PurgeOnceAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "KYC retention job l?i");
                    }

                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // app ðang t?t
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
                                && !e.LegalHold // data under legal hold is not expired
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
                        purgedInBatch++;

                        // evidence of the purge (time, data type, basis) without any personal data
                        var types = "id_images,face_image,ocr_data,biometric"
                                    + (settings.KeepDocumentHashAfterDeletion ? "" : ",document_number");
                        try
                        {
                            await accessLog.LogAsync(record.UserId, record.EkycId, null, KycActorTypes.System,
                                KycAccessActions.RetentionPurge, null,
                                $"purpose=retention_expiry; types={types}; basis=retention_policy_{settings.RetentionDays}d");
                        }
                        catch (Exception logEx) when (logEx is not OperationCanceledException)
                        {
                            // The data is already gone; a failed log write must not be counted as a failed purge
                            _logger.LogError(logEx, "Không ghi ðý?c b?ng ch?ng purge cho ekyc {EkycId}", record.EkycId);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Không xóa ðý?c d? li?u KYC c?a ekyc {EkycId}", record.EkycId);
                    }
                }

                total += purgedInBatch;
                // Có b?n ghi l?i (s? xu?t hi?n l?i ? ð?u batch) ho?c h?t d? li?u th? d?ng, ch? l?n ch?y sau
                if (purgedInBatch < candidates.Count || candidates.Count < BatchSize)
                    break;
            }

            if (total > 0)
                _logger.LogInformation("KYC retention: ð? xóa ?nh/d? li?u OCR c?a {Count} h? sõ (quá {Days} ngày)", total, settings.RetentionDays);
        }
    }
}