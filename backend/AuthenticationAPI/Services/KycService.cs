using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Providers;
using AuthenticationAPI.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace AuthenticationAPI.Services
{

    public class EkycLockedException : Exception
    {
        public DateTime LockedUntilUtc { get; }

        public EkycLockedException(DateTime lockedUntilUtc) : base("eKYC verification is temporarily locked")
        {
            LockedUntilUtc = lockedUntilUtc;
        }
    }

    public class KycService : IKycService
    {
        /// <summary></summary>
        public const int MaxConsecutiveFailures = 3;
        public static readonly TimeSpan LockDuration = TimeSpan.FromHours(24);
        private const string SystemErrorReason = "System error during verification";

        private readonly IEkycProvider _provider;
        private readonly IEkycRepository _repository;
        private readonly IObjectStorage _objectStorage;
        private readonly ICccdDataProtector _cccdProtector;
        private readonly IConfiguration _config;
        private readonly IKycAccessLogService _accessLog;
        private readonly ILogger<KycService> _logger;

        private const decimal MatchScoreThreshold = 90m;
        private const string OcrFailedMessage = "We could not read your ID card. Please retake the photos with good lighting and no glare.";
        private const string FaceFailedMessage = "We could not confirm that the photo matches your ID card. Please retake the selfie in a well-lit place.";
        private const decimal ManualReviewThreshold = 70m;
        private const string DuplicateCccdMessage = "This document has already been used to verify another account";

        public KycService(
            IEkycProvider provider,
            IEkycRepository repository,
            IObjectStorage objectStorage,
            ICccdDataProtector cccdProtector,
            IConfiguration config,
            IKycAccessLogService accessLog,
            ILogger<KycService> logger)
        {
            _accessLog = accessLog;
            _logger = logger;
            _provider = provider;
            _repository = repository;
            _objectStorage = objectStorage;
            _cccdProtector = cccdProtector;
            _config = config;
        }

        public async Task<EkycSubmitResponseDto> SubmitAsync(EkycSubmitRequestDto request, int userId, string? ip = null)
        {
            // Already verified / pending review: return the previous result, don't call VNPT (avoids cost + avoids switching documents)
            var existing = await _repository.GetActiveByUserAsync(userId);
            if (existing is not null)
            {
                return new EkycSubmitResponseDto
                {
                    EkycId = existing.EkycId,
                    Status = existing.Status,
                    Message = existing.Status == "Passed"
                        ? "Your account has already been verified"
                        : "Your submission is pending review"
                };
            }

            // 3 consecutive failures lock eKYC for 24 hours
            await EnsureNotLockedAsync(userId);

            var record = new EkycVerification
            {
                UserId = userId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                ConsentVersion = request.ConsentVersion,
                ConsentedAt = DateTime.UtcNow   // proof of consent (the controller already blocks if consent was not given)
            };
            record.EkycId = await _repository.CreateAsync(record);

            try
            {
                return await ProcessAsync(record, request, ip);
            }
            catch (Exception)
            {
                // Avoid leaving the record stuck at "Pending" when the provider/storage/DB throws
                await MarkFailedSafeAsync(record, SystemErrorReason);
                throw; // the controller will log it and return 500
            }
        }

        private async Task<EkycSubmitResponseDto> ProcessAsync(EkycVerification record, EkycSubmitRequestDto request, string? ip)
        {
            EkycIdentityInfoDto? identity = null;

            // Copy to MemoryStream immediately - avoids depending on the lifetime of Kestrel's internal stream
            await using var frontStream = new MemoryStream();
            await request.CccdFrontImage.CopyToAsync(frontStream);

            await using var backStream = new MemoryStream();
            await request.CccdBackImage.CopyToAsync(backStream);

            await using var selfieStream = new MemoryStream();
            await request.SelfieImage.CopyToAsync(selfieStream);

            // log the collection of the data and the purpose
            await _accessLog.LogAsync(record.UserId, record.EkycId, record.UserId, KycActorTypes.User,
                KycAccessActions.Submit, ip, $"purpose=identity_verification; consentVersion={record.ConsentVersion}");

            // -- Upload original images to object storage ---
            frontStream.Position = 0;
            record.CccdFrontObjectKey = await _objectStorage.UploadAsync(frontStream, $"kyc/{record.EkycId}/front.jpg");

            backStream.Position = 0;
            record.CccdBackObjectKey = await _objectStorage.UploadAsync(backStream, $"kyc/{record.EkycId}/back.jpg");

            selfieStream.Position = 0;
            record.FaceCaptureObjectKey = await _objectStorage.UploadAsync(selfieStream, $"kyc/{record.EkycId}/selfie.jpg");

            // -- Card liveness + CCCD OCR (VNPT) ---
            frontStream.Position = 0;
            backStream.Position = 0;
            await _accessLog.LogAsync(record.UserId, record.EkycId, null, KycActorTypes.System,
                KycAccessActions.SharedWithProvider, ip, "purpose=identity_verification; provider=VNPT; data=id_card_front,id_card_back");
            var ocrResult = await _provider.ExtractIdCardInfoAsync(frontStream, backStream);

            if (!ocrResult.Success)
            {
                // Provider error text stays in the log; the customer gets a stable message.
                _logger.LogWarning("OCR failed for eKYC {EkycId}: {Error}", record.EkycId, ocrResult.ErrorMessage);
                return await FailAsync(record, OcrFailedMessage);
            }

            var idNumber = ocrResult.IdNumber ?? string.Empty;
            // identity comes from the ID card only; the user just reviews it (ID number masked)
            identity = new EkycIdentityInfoDto
            {
                FullName = ocrResult.FullName,
                DateOfBirth = ocrResult.DateOfBirth,
                IdNumberMasked = OcrDataSanitizer.MaskId(idNumber)
            };
            record.CccdNumberEncrypted = _cccdProtector.Protect(idNumber);
            record.CccdNumberHash = _cccdProtector.Hash(idNumber);
            record.OcrRawData = OcrDataSanitizer.Sanitize(ocrResult.RawResponseJson); // CCCD number already masked

           
            if (await _repository.IsCccdUsedByAnotherUserAsync(record.CccdNumberHash, record.UserId))
                return await FailAsync(record, DuplicateCccdMessage, identity);

            frontStream.Position = 0;
            selfieStream.Position = 0;
            await _accessLog.LogAsync(record.UserId, record.EkycId, null, KycActorTypes.System,
                KycAccessActions.SharedWithProvider, ip, "purpose=identity_verification; provider=VNPT; data=id_card_front,selfie");
            var faceResult = await _provider.CompareFaceAsync(frontStream, selfieStream);

            if (!faceResult.Success)
            {
                // CompareFaceAsync only reports Success = false on a provider/timeout error,
                // i.e. a system fault. It must not count towards the user's 3 failed attempts
                // (EnsureNotLockedAsync ignores SystemErrorReason), so it is recorded as such.
                _logger.LogWarning("Face match failed for eKYC {EkycId}: {Error}", record.EkycId, faceResult.ErrorMessage);
                return await FailAsync(record, SystemErrorReason, identity);
            }

            record.FaceMatchScore = faceResult.MatchScore;
            record.LivenessScore = faceResult.LivenessScore;
            record.LivenessPassed = faceResult.LivenessPassed;

            if (faceResult.MatchScore >= MatchScoreThreshold && faceResult.LivenessPassed)
            {
                record.Status = "Passed";
                record.VerifiedAt = DateTime.UtcNow;
            }
            else if (faceResult.MatchScore >= ManualReviewThreshold && faceResult.LivenessPassed)
            {
                // Only allow manual review when the selfie is a real person; if liveness fails, reject outright
                record.Status = "ManualReview";
            }
            else
            {
                record.Status = "Failed";
                record.FailReason = "Face match score or liveness did not meet the threshold";
            }

            try
            {
                // Update the record + User.EkycStatus in a single save (same transaction)
                await _repository.UpdateAsync(record, record.Status);
            }
            catch (DuplicateRecordException)
            {
                // Two accounts passed the duplicate check at the same time: the DB unique index blocks the later one
                return await FailAsync(record, DuplicateCccdMessage, identity);
            }

            return new EkycSubmitResponseDto
            {
                EkycId = record.EkycId,
                Status = record.Status,
                Message = record.Status switch
                {
                    "Passed" => "Verification successful",
                    "ManualReview" => "Your submission is pending review",
                    _ => record.FailReason
                },
                Identity = identity
            };
        }

        public async Task<EkycStatusResponseDto> GetStatusAsync(int ekycId, int userId)
        {
            var record = await _repository.GetByIdAsync(ekycId)
                ?? throw new KeyNotFoundException("eKYC record not found");

            if (record.UserId != userId)
                throw new UnauthorizedAccessException("You do not have permission to view this record");

            var dto = new EkycStatusResponseDto
            {
                EkycId = record.EkycId,
                Status = record.Status,
                FaceMatchScore = record.FaceMatchScore,
                FailReason = record.FailReason
            };

            if (record.Status == "Passed")
                dto.VerifiedToken = GenerateVerifiedToken(record.UserId, record.EkycId);

            return dto;
        }

      
        private async Task EnsureNotLockedAsync(int userId)
        {
            var recent = await _repository.GetRecentByUserAsync(userId, 60);
            var attempts = recent.Where(r => r.FailReason != SystemErrorReason).ToList();

            var streak = 0;
            foreach (var r in attempts)
            {
                if (r.Status == "Failed") streak++;
                else break;
            }

            if (streak == 0 || streak % MaxConsecutiveFailures != 0)
                return;

            var unlockAt = attempts[0].CreatedAt + LockDuration;
            if (unlockAt > DateTime.UtcNow)
                throw new EkycLockedException(unlockAt);
        }

        private async Task<EkycSubmitResponseDto> FailAsync(EkycVerification record, string reason, EkycIdentityInfoDto? identity = null)
        {
            record.Status = "Failed";
            record.FailReason = reason;
            record.VerifiedAt = null;
            await PurgeImagesAsync(record);

            await _repository.UpdateAsync(record, "Failed");
            return new EkycSubmitResponseDto { EkycId = record.EkycId, Status = "Failed", Message = reason, Identity = identity };
        }

        private async Task PurgeImagesAsync(EkycVerification record)
        {
            foreach (var key in new[] { record.CccdFrontObjectKey, record.CccdBackObjectKey, record.FaceCaptureObjectKey })
            {
                if (string.IsNullOrEmpty(key)) continue;
                try { await _objectStorage.DeleteAsync(key); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete image {Key} of failed eKYC {EkycId}", key, record.EkycId); }
            }

            record.CccdFrontObjectKey = null;
            record.CccdBackObjectKey = null;
            record.FaceCaptureObjectKey = null;
            record.OcrRawData = null;
            record.FaceVector = null;
        }

        private async Task MarkFailedSafeAsync(EkycVerification record, string reason)
        {
            try
            {
                record.Status = "Failed";
                record.FailReason = reason;
                await _repository.UpdateAsync(record);
            }
            catch
            {
                // Don't let a DB write error mask the original exception
            }
        }

        private string GenerateVerifiedToken(int userId, int ekycId)
        {
            // Shares the secret with AuthenticationAPI's JWT (JwtSettings:SecretKey in appsettings) 
            // so TicketingService can verify it with the same key.
            var secret = _config["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("Missing configuration JwtSettings:SecretKey");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("userId", userId.ToString()),
                new Claim("ekycId", ekycId.ToString()),
                new Claim("purpose", "ticket_purchase_verification")
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public interface IObjectStorage
    {
        Task<string> UploadAsync(Stream content, string objectKey);

        /// <summary>Reads (and decrypts) a file. Returns null if it does not exist.</summary>
        Task<Stream?> DownloadAsync(string objectKey);

        /// <summary>Deletes a file. Does not throw if the file does not exist.</summary>
        Task DeleteAsync(string objectKey);
    }

    // Named differently (not "IDataProtector") so it doesn't clash with
    // Microsoft.AspNetCore.DataProtection.IDataProtector, which is built into .NET.
    public interface ICccdDataProtector
    {
        string Protect(string plainText);
        string Unprotect(string cipherText);

        /// <summary>Deterministic hash (HMAC-SHA256, hex) of the document number, used for duplicate checks.</summary>
        string Hash(string plainText);
    }
}