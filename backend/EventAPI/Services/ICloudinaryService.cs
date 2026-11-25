namespace EventAPI.Services
{
    public class CloudinaryUploadResult
    {
        public string SecureUrl { get; set; } = null!;
        public string PublicId { get; set; } = null!;
    }

    public interface ICloudinaryService
    {
        Task<CloudinaryUploadResult> UploadImageAsync(Stream fileStream, string fileName, string folder);

        Task DeleteImageAsync(string? publicId);
    }
}
