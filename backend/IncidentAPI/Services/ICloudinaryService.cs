using Microsoft.AspNetCore.Http;

namespace IncidentAPI.Services;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> UploadFileAsync(IFormFile file, string folder, CancellationToken ct = default);

    Task<bool> DeleteFileAsync(string publicId, CancellationToken ct = default);
}

public class CloudinaryUploadResult
{
    public string FileUrl { get; set; } = null!;
    public string PublicId { get; set; } = null!;
    public string ResourceType { get; set; } = "image";
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
}
