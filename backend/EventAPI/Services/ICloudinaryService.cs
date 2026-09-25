namespace EventAPI.Services
{
    public class CloudinaryUploadResult
    {
        public string SecureUrl { get; set; } = null!;
        public string PublicId { get; set; } = null!;
    }

    public interface ICloudinaryService
    {

        // Uploads an image stream to Cloudinary under the given folder.
        // Throws InvalidOperationException with a friendly message on failure.

        Task<CloudinaryUploadResult> UploadImageAsync(Stream fileStream, string fileName, string folder);

        // Deletes an asset from Cloudinary by its public_id. Safe to call with null/empty (no-op).

        Task DeleteImageAsync(string? publicId);
    }
}
