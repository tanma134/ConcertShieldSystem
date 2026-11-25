using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthenticationAPI.Services
{
    public record UploadedDocumentResult(string SecureUrl, string? PreviewUrl, string PublicId, bool IsImage);

    public interface IDocumentStorageService
    {
        Task<UploadedDocumentResult> UploadDocumentAsync(IFormFile file, string documentType);
    }

    public sealed class DocumentStorageService : IDocumentStorageService
    {
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private const string PdfExtension = ".pdf";
        private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentStorageService> _logger;

        public DocumentStorageService(IConfiguration configuration, ILogger<DocumentStorageService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<UploadedDocumentResult> UploadDocumentAsync(IFormFile file, string documentType)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Tệp tải lên không được để trống.");

            if (file.Length > MaxBytes)
                throw new ArgumentException($"Dung lượng tệp '{file.FileName}' vượt quá giới hạn tối đa cho phép (10 MB).");

            var ext = Path.GetExtension(file.FileName);
            bool isImage = AllowedImageExtensions.Contains(ext);
            bool isPdf = string.Equals(ext, PdfExtension, StringComparison.OrdinalIgnoreCase);

            if (!isImage && !isPdf)
                throw new ArgumentException($"Định dạng tệp '{file.FileName}' không được hỗ trợ. Vui lòng chọn tệp ảnh (JPG, PNG, WEBP) hoặc tệp PDF.");

            var cloudName = _configuration["CloudinaryOrganizer:CloudName"] ?? _configuration["Cloudinary:CloudName"];
            var apiKey = _configuration["CloudinaryOrganizer:ApiKey"] ?? _configuration["Cloudinary:ApiKey"];
            var apiSecret = _configuration["CloudinaryOrganizer:ApiSecret"] ?? _configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                _logger.LogError("Cloudinary configuration is missing.");
                throw new InvalidOperationException("Dịch vụ lưu trữ Cloudinary chưa được cấu hình.");
            }

            var cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
            await using var stream = file.OpenReadStream();

            // Both images and PDFs are uploaded with ImageUploadParams so Cloudinary can generate
            // high-quality JPG previews of page 1, which are 100% accessible publicly without 401 ACL errors.
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "concertshield/organizer_documents",
                UseFilename = false,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await cloudinary.UploadAsync(uploadParams);
            if (result.Error != null || string.IsNullOrWhiteSpace(result.SecureUrl?.ToString()))
            {
                _logger.LogError("Cloudinary upload failed for {FileName}: {Error}", file.FileName, result.Error?.Message);
                throw new InvalidOperationException(result.Error?.Message ?? "Không thể tải tệp lên Cloudinary.");
            }

            var secureUrl = result.SecureUrl.ToString();
            // For PDF files, generate JPG preview url of page 1 so it's always viewable
            var previewUrl = isPdf
                ? (secureUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? secureUrl[..^4] + ".jpg" : secureUrl + ".jpg")
                : secureUrl;

            return new UploadedDocumentResult(secureUrl, previewUrl, result.PublicId, isImage);
        }
    }
}
