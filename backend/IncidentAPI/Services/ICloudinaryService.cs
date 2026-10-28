using Microsoft.AspNetCore.Http;

namespace IncidentAPI.Services;

// Interface cho việc upload file lên Cloudinary
// Dùng để dễ mock trong unit test mà không cần gọi Cloudinary thật
public interface ICloudinaryService
{
    // Upload file lên Cloudinary và trả về URL + Public ID
    // folder: thư mục trên Cloudinary để tổ chức file (vd: "incidents/evidence")
    Task<CloudinaryUploadResult> UploadFileAsync(IFormFile file, string folder, CancellationToken ct = default);

    // Xóa file trên Cloudinary khi bằng chứng bị xóa
    Task<bool> DeleteFileAsync(string publicId, CancellationToken ct = default);
}

// Kết quả upload lên Cloudinary
public class CloudinaryUploadResult
{
    // URL đầy đủ để truy cập file
    public string FileUrl { get; set; } = null!;
    // Public ID trên Cloudinary (dùng để xóa sau này)
    public string PublicId { get; set; } = null!;
    // Loại resource: image, video, raw
    public string ResourceType { get; set; } = "image";
    // Kích thước file (bytes)
    public long FileSize { get; set; }
    // Loại MIME
    public string? MimeType { get; set; }
}
