using System.Security.Cryptography;
using System.Text;

namespace AuthenticationAPI.Services
{
    public class AesDataProtector : ICccdDataProtector
    {
        private readonly byte[] _key;
        private readonly byte[] _hmacKey;

        public AesDataProtector(IConfiguration config)
        {
            var raw = config["DataProtection:AesKey"]
                ?? throw new InvalidOperationException("Thiếu cấu hình DataProtection:AesKey (phải đúng 32 ký tự)");
            _key = Encoding.UTF8.GetBytes(raw.PadRight(32).Substring(0, 32));

            var hmacRaw = config["DataProtection:HmacKey"]
                ?? throw new InvalidOperationException("Thiếu cấu hình DataProtection:HmacKey (tối thiểu 32 ký tự, khác AesKey)");
            if (hmacRaw.Length < 32)
                throw new InvalidOperationException("DataProtection:HmacKey phải có tối thiểu 32 ký tự");
            _hmacKey = Encoding.UTF8.GetBytes(hmacRaw);
        }

        public string Protect(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string Unprotect(string cipherText)
        {
            var fullBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = new byte[16];
            Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = new byte[fullBytes.Length - iv.Length];
            Buffer.BlockCopy(fullBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);

            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        // Hash cố định (HMAC-SHA256) để so sánh trùng CCCD. AES ở trên dùng IV ngẫu nhiên nên không so sánh được.
        public string Hash(string plainText)
        {
            var normalized = new string(plainText.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            using var hmac = new HMACSHA256(_hmacKey);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized)));
        }
    }
}