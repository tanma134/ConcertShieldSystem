using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IncidentAPI.Services;

// Triển khai thực tế của ICloudinaryService dùng CloudinaryDotNet SDK
// Lưu bằng chứng sự cố vào thư mục "incidents/evidence" trên Cloudinary
public class CloudinaryService : ICloudinaryService
{
    // Giới hạn kích thước file tối đa: 20MB để tránh lạm dụng
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;

    // Các loại MIME được chấp nhận: ảnh, video, tài liệu
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

        // Đọc cấu hình Cloudinary từ appsettings.json
        var cloudName = config["Cloudinary:CloudName"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Cloudinary:CloudName");
        var apiKey = config["Cloudinary:ApiKey"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Cloudinary:ApiKey");
        var apiSecret = config["Cloudinary:ApiSecret"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Cloudinary:ApiSecret");

        // Khởi tạo Cloudinary client với credentials
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true; // Luôn dùng HTTPS
    }

    // Upload file lên Cloudinary
    // Xác thực file trước, sau đó upload và trả về URL + Public ID
    public async Task<CloudinaryUploadResult> UploadFileAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        // Kiểm tra file không được null hoặc rỗng
        if (file == null || file.Length == 0)
            throw new IncidentServiceException(400, "File không hợp lệ hoặc rỗng.");

        // Kiểm tra kích thước file không vượt giới hạn
        if (file.Length > MaxFileSizeBytes)
            throw new IncidentServiceException(400, $"File vượt quá giới hạn {MaxFileSizeBytes / 1024 / 1024}MB.");

        // Kiểm tra loại MIME có được phép không
        var mimeType = file.ContentType?.ToLowerInvariant() ?? "";
        if (!AllowedMimeTypes.Contains(mimeType))
            throw new IncidentServiceException(400, $"Loại file không được hỗ trợ: {mimeType}. Chỉ chấp nhận ảnh, video, PDF và audio.");

        // Xác định resource type dựa trên MIME
        var resourceType = mimeType.StartsWith("image/") ? ResourceType.Image
            : mimeType.StartsWith("video/") ? ResourceType.Video
            : ResourceType.Raw;

        // Đặt tên public ID an toàn, tránh ký tự đặc biệt
        var safeFileName = Path.GetFileNameWithoutExtension(file.FileName ?? "evidence")
            .Replace(" ", "_")
            .Replace(".", "_");
        var publicId = $"{folder}/{DateTime.UtcNow:yyyyMMddHHmmss}_{safeFileName}";

        try
        {
            using var stream = file.OpenReadStream();

            // Tạo upload params tùy theo loại resource
            if (resourceType == ResourceType.Image)
            {
                var imageParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = publicId,
                    Overwrite = false,
                    // Tự động xoay theo EXIF metadata
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
                // PDF, Audio - upload như raw file
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
            // Ném lại lỗi đã được xử lý
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi upload file lên Cloudinary: {FileName}", file.FileName);
            throw new IncidentServiceException(502, "Không thể upload file lên Cloudinary. Vui lòng thử lại.");
        }
    }

    // Xóa file trên Cloudinary theo Public ID
    // Trả về true nếu xóa thành công, false nếu không tìm thấy
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
