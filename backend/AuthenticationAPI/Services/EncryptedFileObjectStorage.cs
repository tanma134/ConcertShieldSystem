using System.Security.Cryptography;
using System.Text;

namespace AuthenticationAPI.Services
{
    /// <summary>
    /// Lưu ảnh KYC ra đĩa dưới dạng đã mã hóa AES-256-GCM.
    /// Định dạng file: "ENC1" (4 byte) | nonce (12) | tag (16) | ciphertext.
    /// objectKey được dùng làm dữ liệu xác thực kèm theo (AAD): chép file sang tên khác sẽ giải mã thất bại.
    /// </summary>
    public class EncryptedFileObjectStorage : IObjectStorage
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ENC1");
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly string _basePath;
        private readonly byte[] _key;

        public EncryptedFileObjectStorage(IWebHostEnvironment env, IConfiguration config)
        {
            var raw = config["DataProtection:FileKey"]
                ?? throw new InvalidOperationException("Thiếu cấu hình DataProtection:FileKey (tối thiểu 32 ký tự, khác AesKey/HmacKey)");
            if (raw.Length < 32)
                throw new InvalidOperationException("DataProtection:FileKey phải có tối thiểu 32 ký tự");

            _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

            // Nên đặt thư mục NGOÀI thư mục source/wwwroot ở production, ví dụ D:\secure\kyc
            var configured = config["Storage:KycPath"];
            _basePath = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "kyc-uploads")
                : configured;
            Directory.CreateDirectory(_basePath);
        }

        public async Task<string> UploadAsync(Stream content, string objectKey)
        {
            var fullPath = ResolvePath(objectKey);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            var plain = ms.ToArray();

            // ===== TẠM COMMENT ĐỂ TEST CHỤP ẢNH - NHỚ BỎ COMMENT LẠI SAU KHI TEST XONG =====
            // var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            // var cipher = new byte[plain.Length];
            // var tag = new byte[TagSize];
            //
            // using (var gcm = new AesGcm(_key, TagSize))
            // {
            //     gcm.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(objectKey));
            // }
            //
            // await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            // await fs.WriteAsync(Magic);
            // await fs.WriteAsync(nonce);
            // await fs.WriteAsync(tag);
            // await fs.WriteAsync(cipher);

            // --- Ghi thẳng file gốc, không mã hóa (CHỈ DÙNG ĐỂ TEST) ---
            await File.WriteAllBytesAsync(fullPath, plain);
            // ===== HẾT PHẦN TẠM COMMENT =====

            return objectKey;
        }

        public async Task<Stream?> DownloadAsync(string objectKey)
        {
            var fullPath = ResolvePath(objectKey);
            if (!File.Exists(fullPath))
                return null;

            var data = await File.ReadAllBytesAsync(fullPath);

            // Không dùng Span<byte> làm biến cục bộ trong method async (lỗi CS4012),
            // nên phần kiểm tra header + giải mã được tách sang method đồng bộ.
            if (!IsEncrypted(data))
                return new MemoryStream(data); // file cũ chưa mã hóa (từ lúc dev)

            var plain = Decrypt(data, objectKey);
            return new MemoryStream(plain);
        }

        public Task DeleteAsync(string objectKey)
        {
            var fullPath = ResolvePath(objectKey);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            // dọn thư mục kyc/{id} nếu đã trống
            var dir = Path.GetDirectoryName(fullPath);
            if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);

            return Task.CompletedTask;
        }

        // Kiểm tra file có header "ENC1" hay không (method đồng bộ nên dùng Span thoải mái)
        private static bool IsEncrypted(byte[] data)
        {
            var headerSize = Magic.Length + NonceSize + TagSize;
            return data.Length >= headerSize
                && data.AsSpan(0, Magic.Length).SequenceEqual(Magic);
        }

        // Giải mã: ENC1 | nonce | tag | ciphertext
        private byte[] Decrypt(byte[] data, string objectKey)
        {
            var headerSize = Magic.Length + NonceSize + TagSize;

            var nonce = data.AsSpan(Magic.Length, NonceSize);
            var tag = data.AsSpan(Magic.Length + NonceSize, TagSize);
            var cipher = data.AsSpan(headerSize);
            var plain = new byte[cipher.Length];

            using var gcm = new AesGcm(_key, TagSize);
            gcm.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(objectKey));

            return plain;
        }

        // Chặn path traversal ("../") khi ghép objectKey vào đường dẫn
        private string ResolvePath(string objectKey)
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, objectKey.Replace('/', Path.DirectorySeparatorChar)));
            var root = Path.GetFullPath(_basePath) + Path.DirectorySeparatorChar;

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("objectKey không hợp lệ");

            return full;
        }
    }
}