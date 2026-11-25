using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IncidentAPI.Services;

public class CloudinaryService : ICloudinaryService
{
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "video/mp4", "video/quicktime", "video/x-msvideo",
        "application/pdf",
        "audio/mpeg", "audio/wav"
    };

    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
    {
        _logger = logger;

        var cloudName = config["Cloudinary:CloudName"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Cloudinary:CloudName");
        var apiKey = config["Cloudinary:ApiKey"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Cloudinary:ApiKey");
        var apiSecret = config["Cloudinary:ApiSecret"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Cloudinary:ApiSecret");

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<CloudinaryUploadResult> UploadFileAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            throw new IncidentServiceException(400, "File không hợp lệ hoặc rỗng.");

        if (file.Length > MaxFileSizeBytes)
            throw new IncidentServiceException(400, $"File vượt quá giới hạn {MaxFileSizeBytes / 1024 / 1024}MB.");

        var mimeType = file.ContentType?.ToLowerInvariant() ?? "";
        if (!AllowedMimeTypes.Contains(mimeType))
            throw new IncidentServiceException(400, $"Loại file không được hỗ trợ: {mimeType}. Chỉ chấp nhận ảnh, video, PDF và audio.");

        var resourceType = mimeType.StartsWith("image/") ? ResourceType.Image
            : mimeType.StartsWith("video/") ? ResourceType.Video
            : ResourceType.Raw;

        var safeFileName = Path.GetFileNameWithoutExtension(file.FileName ?? "evidence")
            .Replace(" ", "_")
            .Replace(".", "_");
        var publicId = $"{folder}/{DateTime.UtcNow:yyyyMMddHHmmss}_{safeFileName}";

        try
        {
            using var stream = file.OpenReadStream();

            if (resourceType == ResourceType.Image)
            {
                var imageParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = publicId,
                    Overwrite = false,
                    Transformation = new Transformation().Quality("auto").FetchFormat("auto")
                };

                var imageResult = await _cloudinary.UploadAsync(imageParams, ct);
                if (imageResult.Error != null)
                    throw new IncidentServiceException(502, $"Cloudinary lỗi: {imageResult.Error.Message}");

                return new CloudinaryUploadResult
                {
                    FileUrl = imageResult.SecureUrl.ToString(),
                    PublicId = imageResult.PublicId,
                    ResourceType = "image",
                    FileSize = imageResult.Bytes,
                    MimeType = mimeType
                };
            }
            else if (resourceType == ResourceType.Video)
            {
                var videoParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = publicId,
                    Overwrite = false
                };

                var videoResult = await _cloudinary.UploadAsync(videoParams, ct);
                if (videoResult.Error != null)
                    throw new IncidentServiceException(502, $"Cloudinary lỗi: {videoResult.Error.Message}");

                return new CloudinaryUploadResult
                {
                    FileUrl = videoResult.SecureUrl.ToString(),
                    PublicId = videoResult.PublicId,
                    ResourceType = "video",
                    FileSize = videoResult.Bytes,
                    MimeType = mimeType
                };
            }
            else
            {
                var rawParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = publicId,
                    Overwrite = false
                };

                var rawResult = await _cloudinary.UploadAsync(rawParams, "upload", ct);
                if (rawResult.Error != null)
                    throw new IncidentServiceException(502, $"Cloudinary lỗi: {rawResult.Error.Message}");

                return new CloudinaryUploadResult
                {
                    FileUrl = rawResult.SecureUrl.ToString(),
                    PublicId = rawResult.PublicId,
                    ResourceType = "raw",
                    FileSize = rawResult.Bytes,
                    MimeType = mimeType
                };
            }
        }
        catch (IncidentServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi upload file lên Cloudinary: {FileName}", file.FileName);
            throw new IncidentServiceException(502, "Không thể upload file lên Cloudinary. Vui lòng thử lại.");
        }
    }

    public async Task<bool> DeleteFileAsync(string publicId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return false;

        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);
            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể xóa file Cloudinary: {PublicId}", publicId);
            return false;
        }
    }
}
