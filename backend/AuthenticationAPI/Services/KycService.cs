using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Providers;
using AuthenticationAPI.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace AuthenticationAPI.Services
{

    public class KycService : IKycService
    {
        private readonly IEkycProvider _provider;
        private readonly IEkycRepository _repository;
        private readonly IObjectStorage _objectStorage;
        private readonly ICccdDataProtector _cccdProtector;
        private readonly IConfiguration _config;

        private const decimal MatchScoreThreshold = 90m;
        private const decimal ManualReviewThreshold = 70m;
        private const string DuplicateCccdMessage = "This document has already been used to verify another account";

        public KycService(
            IEkycProvider provider,
            IEkycRepository repository,
            IObjectStorage objectStorage,
            ICccdDataProtector cccdProtector,
            IConfiguration config)
        {
            _provider = provider;
            _repository = repository;
            _objectStorage = objectStorage;
            _cccdProtector = cccdProtector;
            _config = config;
        }

        public async Task<EkycSubmitResponseDto> SubmitAsync(EkycSubmitRequestDto request, int userId)
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
                return await ProcessAsync(record, request);
            }
            catch (Exception)
            {
                // Avoid leaving the record stuck at "Pending" when the provider/storage/DB throws
                await MarkFailedSafeAsync(record, "System error during verification");
                throw; // the controller will log it and return 500
            }
        }

        private async Task<EkycSubmitResponseDto> ProcessAsync(EkycVerification record, EkycSubmitRequestDto request)
        {
            // Copy to MemoryStream immediately - avoids depending on the lifetime of Kestrel's internal stream
            await using var frontStream = new MemoryStream();
            await request.CccdFrontImage.CopyToAsync(frontStream);

            await using var backStream = new MemoryStream();
            await request.CccdBackImage.CopyToAsync(backStream);

            await using var selfieStream = new MemoryStream();
            await request.SelfieImage.CopyToAsync(selfieStream);

            // --- Upload original images to object storage ---
            frontStream.Position = 0;
            record.CccdFrontObjectKey = await _objectStorage.UploadAsync(frontStream, $"kyc/{record.EkycId}/front.jpg");

            backStream.Position = 0;
            record.CccdBackObjectKey = await _objectStorage.UploadAsync(backStream, $"kyc/{record.EkycId}/back.jpg");

            selfieStream.Position = 0;
            record.FaceCaptureObjectKey = await _objectStorage.UploadAsync(selfieStream, $"kyc/{record.EkycId}/selfie.jpg");

            // --- Card liveness + CCCD OCR (VNPT) ---
            frontStream.Position = 0;
            backStream.Position = 0;
            var ocrResult = await _provider.ExtractIdCardInfoAsync(frontStream, backStream);

            if (!ocrResult.Success)
                return await FailAsync(record, $"OCR failed: {ocrResult.ErrorMessage}");

            var idNumber = ocrResult.IdNumber ?? string.Empty;
            record.CccdNumberEncrypted = _cccdProtector.Protect(idNumber);
            record.CccdNumberHash = _cccdProtector.Hash(idNumber);
            record.OcrRawData = OcrDataSanitizer.Sanitize(ocrResult.RawResponseJson); // CCCD number already masked

            // --- Block one CCCD from being used for multiple accounts (checked first to save VNPT face API calls) ---
            if (await _repository.IsCccdUsedByAnotherUserAsync(record.CccdNumberHash, record.UserId))
                return await FailAsync(record, DuplicateCccdMessage);

            // --- Face liveness + Face matching (VNPT) ---
            frontStream.Position = 0;
            selfieStream.Position = 0;
            var faceResult = await _provider.CompareFaceAsync(frontStream, selfieStream);

            if (!faceResult.Success)
                return await FailAsync(record, $"Face match failed: {faceResult.ErrorMessage}");

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
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Two accounts passed the duplicate check at the same time: the DB unique index blocks the later one
                return await FailAsync(record, DuplicateCccdMessage);
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
                }
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

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        private async Task<EkycSubmitResponseDto> FailAsync(EkycVerification record, string reason)
        {
            record.Status = "Failed";
            record.FailReason = reason;
            record.VerifiedAt = null;
            await _repository.UpdateAsync(record, "Failed");
            return new EkycSubmitResponseDto { EkycId = record.EkycId, Status = "Failed", Message = reason };
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
            // Shares the secret with AuthenticationAPI's JWT (JwtSettings:SecretKey in appsettings) —
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