
using AuthenticationAPI.Models;

namespace AuthenticationAPI.Services
{
    public class KycDataPurger : IKycDataPurger
    {
        private readonly AuthenticationDbContext _db;
        private readonly IObjectStorage _storage;

        public KycDataPurger(AuthenticationDbContext db, IObjectStorage storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task PurgeAsync(EkycVerification record, bool keepDocumentHash, CancellationToken ct = default)
        {
            if (record.LegalHold)
                throw new InvalidOperationException($"eKYC record {record.EkycId} is under legal hold and cannot be purged");

            foreach (var key in new[] { record.CccdFrontObjectKey, record.CccdBackObjectKey, record.FaceCaptureObjectKey })
            {
                if (!string.IsNullOrEmpty(key))
                    await _storage.DeleteAsync(key);
            }

            record.CccdFrontObjectKey = null;
            record.CccdBackObjectKey = null;
            record.FaceCaptureObjectKey = null;
            record.OcrRawData = null;
            record.FaceVector = null;
            record.DataPurgedAt ??= DateTime.UtcNow;

            if (!keepDocumentHash)
            {
                record.CccdNumberHash = null;
                record.CccdNumberEncrypted = null;
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}