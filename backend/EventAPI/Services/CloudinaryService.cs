using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace EventAPI.Services
{

    // Wraps the Cloudinary SDK. Reads credentials from configuration section "Cloudinary"
    // (CloudName / ApiKey / ApiSecret) — never hardcode the secret, never return it to clients.

    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryService> _logger;

        // Basic guardrails so this can't be abused as a generic file-upload endpoint.
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public CloudinaryService(IConfiguration configuration, ILogger<CloudinaryService> logger)
        {
            _logger = logger;

            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                throw new InvalidOperationException(
                    "Cloudinary is not configured. Set Cloudinary:CloudName, Cloudinary:ApiKey, Cloudinary:ApiSecret in appsettings.json.");
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<CloudinaryUploadResult> UploadImageAsync(Stream fileStream, string fileName, string folder)
        {
            var ext = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException($"File type '{ext}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

            if (fileStream.Length > MaxFileSizeBytes)
                throw new InvalidOperationException($"File is too large. Max size is {MaxFileSizeBytes / (1024 * 1024)}MB.");

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = $"concertshield/{folder}",
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            ImageUploadResult result;
            try
            {
                result = await _cloudinary.UploadAsync(uploadParams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary upload failed for {FileName}", fileName);
                throw new InvalidOperationException("Image upload to Cloudinary failed. Please try again.", ex);
            }

            if (result.Error != null)
            {
                _logger.LogError("Cloudinary upload error for {FileName}: {Error}", fileName, result.Error.Message);
                throw new InvalidOperationException($"Cloudinary upload error: {result.Error.Message}");
            }

            if (string.IsNullOrEmpty(result.SecureUrl?.ToString()) || string.IsNullOrEmpty(result.PublicId))
                throw new InvalidOperationException("Cloudinary returned an unexpected response (missing url/public_id).");

            return new CloudinaryUploadResult
            {
                SecureUrl = result.SecureUrl.ToString(),
                PublicId = result.PublicId
            };
        }

        public async Task DeleteImageAsync(string? publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return;

            try
            {
                var deleteParams = new DeletionParams(publicId) { ResourceType = ResourceType.Image };
                var result = await _cloudinary.DestroyAsync(deleteParams);
                if (result.Result != "ok" && result.Result != "not found")
                {
                    _logger.LogWarning("Cloudinary deletion for {PublicId} returned: {Result}", publicId, result.Result);
                }
            }
            catch (Exception ex)
            {
                // Deletion failures shouldn't block the caller's DB operation — log and move on.
                _logger.LogError(ex, "Cloudinary deletion failed for {PublicId}", publicId);
            }
        }
    }
}
