using AuthenticationAPI.Repositories;

namespace AuthenticationAPI.Services
{
    /// <summary>
    /// 3.5 - Automatically deletes the original images and OCR data after N days (N comes from kyc_settings).
    /// Kept: status, scores, timestamps; the CCCD hash / encrypted number depend on KeepDocumentHashAfterDeletion.
    /// Records that are Pending or ManualReview are never touched.
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
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // wait for the app to finish starting

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await PurgeOnceAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "KYC retention job failed");
                    }

                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // application is shutting down
            }
        }

        private async Task PurgeOnceAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var settings = await sp.GetRequiredService<IKycSettingService>().GetAsync();

            if (settings.RetentionDays <= 0)
                return;

            var ekycRepository = sp.GetRequiredService<IEkycRepository>();
            var purger = sp.GetRequiredService<IKycDataPurger>();
            var accessLog = sp.GetRequiredService<IKycAccessLogService>();

            var cutoff = DateTime.UtcNow.AddDays(-settings.RetentionDays);
            var total = 0;

            while (!ct.IsCancellationRequested)
            {
                var candidates = await ekycRepository.GetPurgeCandidatesAsync(cutoff, BatchSize, ct);

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
                            _logger.LogError(logEx, "Could not write the purge evidence for ekyc {EkycId}", record.EkycId);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Could not delete the KYC data of ekyc {EkycId}", record.EkycId);
                    }
                }

                total += purgedInBatch;
                // Some records failed (they would show up again at the start of the next batch) or the data ran out: wait for the next run
                if (purgedInBatch < candidates.Count || candidates.Count < BatchSize)
                    break;
            }

            if (total > 0)
                _logger.LogInformation("KYC retention: deleted images/OCR data of {Count} records (older than {Days} days)", total, settings.RetentionDays);
        }
    }
}