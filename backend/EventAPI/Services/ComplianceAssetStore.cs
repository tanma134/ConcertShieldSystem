using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace EventAPI.Services;
public record StoredComplianceAsset(string PublicId, string SecureUrl);
public interface IComplianceAssetStore
{
    Task<StoredComplianceAsset> UploadAsync(byte[] bytes, string publicId, CancellationToken ct);
    Task<byte[]> DownloadAsync(string publicId, CancellationToken ct);
    Task DeleteAsync(string publicId, CancellationToken ct);
}
public class ComplianceAssetStore(HttpClient http, IConfiguration config, ILogger<ComplianceAssetStore> logger) : IComplianceAssetStore
{
    private string Required(string key)
    {
        var value = config["Cloudinary:" + key]?.Trim();
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"Missing Cloudinary:{key} configuration.");
        return value;
    }

    // Chỉ log error.message đã làm sạch; không log response/request đầy đủ hay chữ ký.
    private string ProviderReason(string body)
    {
        string reason;
        try
        {
            using var json = JsonDocument.Parse(body);
            reason = json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
                ? message.GetString()! : "Provider returned no error message.";
        }
        catch (JsonException) { reason = "Provider returned a non-JSON error response."; }
        foreach (var key in new[] { "ApiKey", "ApiSecret" })
        {
            var value = config["Cloudinary:" + key]?.Trim();
            if (!string.IsNullOrEmpty(value)) reason = reason.Replace(value, "[redacted]", StringComparison.Ordinal);
        }
        reason = Regex.Replace(reason, @"(?i)signature\s*[:=]?\s*['"" ]?[a-f0-9]{40,64}", "signature [redacted]");
        reason = Regex.Replace(reason, @"(?i)https?://\S+", "[url redacted]");
        reason = Regex.Replace(reason, @"[\r\n\t]+", " ");
        return reason.Length > 400 ? reason[..400] : reason;
    }
    // Ký tham số REST theo Cloudinary; secret không xuất hiện trong URL hoặc response của API.
    private Dictionary<string, string> Sign(Dictionary<string, string> values)
    {
        var plain = string.Join("&", values.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"));
        values["signature"] = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(plain + Required("ApiSecret")))).ToLowerInvariant();
        values["api_key"] = Required("ApiKey");
        return values;
    }
    private string Endpoint(string action) => $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(Required("CloudName"))}/raw/{action}";
    public async Task<StoredComplianceAsset> UploadAsync(byte[] bytes, string publicId, CancellationToken ct)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            var extension = Path.GetExtension(publicId);
            var values = Sign(new() { ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ["type"] = "authenticated", ["public_id"] = publicId });
            using var form = new MultipartFormDataContent();
            foreach (var item in values) form.Add(new StringContent(item.Value), item.Key);
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(extension.ToLowerInvariant() switch
            { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => "application/octet-stream" });
            form.Add(file, "file", "document" + extension);
            using var result = await http.PostAsync(Endpoint("upload"), form, ct);
            var body = await result.Content.ReadAsStringAsync(ct);
            if (!result.IsSuccessStatusCode)
            {
                var reason = ProviderReason(body);
                logger.LogWarning("Cloudinary compliance upload rejected: HTTP {Status}; {Reason}", (int)result.StatusCode, reason);
                var code = result.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "StorageAuthentication",
                    HttpStatusCode.RequestEntityTooLarge => "StorageFileTooLarge",
                    HttpStatusCode.TooManyRequests => "StorageRateLimit",
                    _ => reason.Contains("signature", StringComparison.OrdinalIgnoreCase) ? "StorageAuthentication" : "StorageRejected"
                };
                throw new AssetUploadException(publicId, code,
                    new HttpRequestException($"Cloudinary HTTP {(int)result.StatusCode}: {reason}", null, result.StatusCode), (int)result.StatusCode);
            }
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("public_id", out var id) || id.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("secure_url", out var url) || url.ValueKind != JsonValueKind.String
                || id.GetString() != publicId || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var assetUrl)
                || assetUrl.Scheme != "https")
                throw new AssetUploadException(publicId, "StorageInvalidResponse");
            // Không ghi metadata thành công nếu provider trả asset public hoặc sai resource type.
            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "authenticated"
                || !root.TryGetProperty("resource_type", out var resource) || resource.ValueKind != JsonValueKind.String || resource.GetString() != "raw")
                throw new AssetUploadException(publicId, "StorageInvalidResponse");
            return new(id.GetString()!, url.GetString()!);
        }
        catch (AssetUploadException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Compliance upload canceled by caller/Gateway after {ElapsedMs} ms; bytes={Bytes}; requestAborted=true", elapsed.ElapsedMilliseconds, bytes.Length);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning("Cloudinary upload timed out after {ElapsedMs} ms; bytes={Bytes}; requestAborted=false", elapsed.ElapsedMilliseconds, bytes.Length);
            throw new AssetUploadException(publicId, "StorageTimeout", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Cloudinary connection failed after {ElapsedMs} ms; bytes={Bytes}; exception={ExceptionType}", elapsed.ElapsedMilliseconds, bytes.Length, ex.GetType().Name);
            throw new AssetUploadException(publicId, "StorageConnection", ex);
        }
        catch (JsonException ex) { throw new AssetUploadException(publicId, "StorageInvalidResponse", ex); }
        catch (InvalidOperationException ex) { throw new AssetUploadException(publicId, "StorageConfiguration", ex); }
    }
    public async Task<byte[]> DownloadAsync(string publicId, CancellationToken ct)
    {
        var values = Sign(new() { ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ["expires_at"] = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString(), ["type"] = "authenticated", ["public_id"] = publicId });
        var url = Endpoint("download") + "?" + string.Join("&", values.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
        return await http.GetByteArrayAsync(url, ct);
    }
    public async Task DeleteAsync(string publicId, CancellationToken ct)
    {
        var values = Sign(new() { ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ["type"] = "authenticated", ["public_id"] = publicId });
        using var result = await http.PostAsync(Endpoint("destroy"), new FormUrlEncodedContent(values), ct);
        result.EnsureSuccessStatusCode();
    }
}
public class AssetUploadException : Exception
{
    public string PublicId { get; }
    public string ErrorCode { get; }
    public int? ProviderStatus { get; }
    public AssetUploadException(string publicId, string errorCode = "StorageRejected", Exception? inner = null, int? providerStatus = null)
        : base(UserMessage(errorCode), inner)
    { PublicId = publicId; ErrorCode = errorCode; ProviderStatus = providerStatus; }

    // Chỉ trả lời hướng xử lý; chi tiết provider đã redacted nằm trong log server.
    private static string UserMessage(string code) => code switch
    {
        "StorageConfiguration" => "Document storage is not configured. Ask the administrator to check its settings.",
        "StorageAuthentication" => "Document storage authentication failed. Ask the administrator to check its credentials.",
        "StorageFileTooLarge" => "Document storage rejected the file size. Try a smaller file.",
        "StorageRateLimit" => "Document storage is busy. Please wait before uploading again.",
        "StorageTimeout" => "Document upload timed out. Please retry when your connection is stable.",
        "StorageConnection" => "Cannot connect to document storage. Please retry or contact the administrator.",
        "StorageInvalidResponse" => "Document storage returned an invalid response. Please contact the administrator.",
        _ => "Document storage rejected the upload. Please contact the administrator."
    };
}
