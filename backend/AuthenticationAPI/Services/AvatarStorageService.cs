using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace AuthenticationAPI.Services;

public interface IAvatarStorageService
{
    Task<(string SecureUrl, string PublicId)> UploadAsync(IFormFile file, string? oldPublicId);
}

public sealed class AvatarStorageService(IConfiguration configuration, ILogger<AvatarStorageService> logger) : IAvatarStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
    private const long MaxBytes = 5 * 1024 * 1024;

    public async Task<(string SecureUrl, string PublicId)> UploadAsync(IFormFile file, string? oldPublicId)
    {
        if (file is null || file.Length == 0) throw new ArgumentException("Avatar file is required.");
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType)) throw new ArgumentException("Only JPG, PNG, and WEBP images are allowed.");
        if (file.Length > MaxBytes) throw new ArgumentException("Avatar file cannot exceed 5 MB.");
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            throw new InvalidOperationException("Cloudinary avatar upload is not configured.");
        var cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
        await using var stream = file.OpenReadStream();
        var result = await cloudinary.UploadAsync(new ImageUploadParams { File = new FileDescription(file.FileName, stream), Folder = "concertshield/avatars", UseFilename = false, UniqueFilename = true, Overwrite = false });
        if (result.Error is not null || string.IsNullOrWhiteSpace(result.SecureUrl?.ToString()) || string.IsNullOrWhiteSpace(result.PublicId))
        {
            logger.LogError("Avatar upload failed: {Error}", result.Error?.Message);
            throw new InvalidOperationException(result.Error?.Message ?? "Avatar upload failed. Please try again.");
        }
        if (!string.IsNullOrWhiteSpace(oldPublicId))
        {
            try { await cloudinary.DestroyAsync(new DeletionParams(oldPublicId) { ResourceType = ResourceType.Image }); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not delete previous avatar {PublicId}", oldPublicId); }
        }
        return (result.SecureUrl.ToString(), result.PublicId);
    }
}
