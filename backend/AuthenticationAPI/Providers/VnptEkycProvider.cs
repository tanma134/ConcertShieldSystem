using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuthenticationAPI.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Written according to VNPT's "eKYC API" documentation (domain https://api.idg.vnpt.vn)
namespace AuthenticationAPI.Providers
{
    public class VnptEkycOptions
    {
        public string BaseUrl { get; set; } = "https://api.idg.vnpt.vn";
        public string AccessToken { get; set; } = "";
        public string TokenId { get; set; } = "";
        public string TokenKey { get; set; } = "";
        public string MacAddress { get; set; } = "AUTH-API-SERVER";


        public string CropParam { get; set; } = "0.14,0.3";

        public bool RejectExpiredCard { get; set; } = true;

        public bool SplitOcr { get; set; } = false;
    }

    public class VnptApiException : Exception
    {
        public string? Code { get; }
        public VnptApiException(string message, string? code = null) : base(message) { Code = code; }
    }

    public static class VnptEkycServiceExtensions
    {
        // Program.cs: builder.Services.AddVnptEkyc(builder.Configuration);
        public static IServiceCollection AddVnptEkyc(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<VnptEkycOptions>(config.GetSection("Vnpt"));

            services.AddHttpClient<IEkycProvider, VnptEkycProvider>((sp, client) =>
            {
                var o = sp.GetRequiredService<IOptions<VnptEkycOptions>>().Value;
                client.BaseAddress = new Uri(o.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(60);
                var accessToken = o.AccessToken.Trim();
                if (accessToken.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase))
                    accessToken = accessToken.Substring(7).Trim();

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Add("Token-id", o.TokenId);
                client.DefaultRequestHeaders.Add("Token-key", o.TokenKey);
                client.DefaultRequestHeaders.Add("mac-address", o.MacAddress);
            });

            return services;
        }
    }

    public class VnptEkycProvider : IEkycProvider
    {
        private const string OkCode = "IDG-00000000";

        private readonly HttpClient _http;
        private readonly VnptEkycOptions _options;
        private readonly ILogger<VnptEkycProvider> _logger;

        private readonly string _clientSession =
            $"ANDROID_backend_28_Device_1.0.0_{Guid.NewGuid():N}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        private readonly Dictionary<string, string> _hashCache = new();

        public VnptEkycProvider(HttpClient http, IOptions<VnptEkycOptions> options, ILogger<VnptEkycProvider> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }
        public async Task<OcrResult> ExtractIdCardInfoAsync(Stream front, Stream back)
        {
            try
            {
                _logger.LogInformation("VNPT OCR started: SplitOcr={Split}, CropParam='{Crop}'",
                    _options.SplitOcr, _options.CropParam);

                var frontHash = await GetHashAsync(front, "front.jpg");
                var backHash = await GetHashAsync(back, "back.jpg");

                var cardLive = (await PostAiAsync("/ai/v1/card/liveness",
                    new() { ["img"] = frontHash }, includeToken: false)).GetProperty("object");

                if (!IsSuccess(cardLive, "liveness")
                    || GetBool(cardLive, "fake_liveness")
                    || GetBool(cardLive, "face_swapping"))
                {
                    return Fail("Document is invalid or suspected to be forged"
                                + (GetString(cardLive, "liveness_msg") is { } m ? $" ({m})" : ""));
                }

                if (_options.SplitOcr)
                    return await ExtractSplitAsync(frontHash, backHash);
                var ocrBody = new Dictionary<string, object?>
                {
                    ["img_front"] = frontHash,
                    ["img_back"] = backHash,
                    ["type"] = -1,
                    ["validate_postcode"] = true
                };
                if (!string.IsNullOrWhiteSpace(_options.CropParam))
                    ocrBody["crop_param"] = _options.CropParam;

                var ocr = await PostAiAsync("/ai/v1/ocr/id", ocrBody);

                var obj = ocr.GetProperty("object");

                var error = ValidateOcr(obj);
                if (error is not null)
                    return Fail(error);

                return new OcrResult
                {
                    Success = true,
                    IdNumber = GetString(obj, "id"),
                    FullName = GetString(obj, "name"),
                    DateOfBirth = GetString(obj, "birth_day"),
                    RawResponseJson = ocr.GetRawText()
                };
            }
            catch (Exception ex) when (ex is VnptApiException or HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "VNPT OCR error");
                return Fail(ToUserMessage(ex));
            }
        }
        private async Task<OcrResult> ExtractSplitAsync(string frontHash, string backHash)
        {
            JsonElement frontObj, backObj;

            try
            {
                frontObj = (await PostAiAsync("/ai/v1/ocr/id/front", new()
                {
                    ["img_front"] = frontHash,
                    ["type"] = -1,
                    ["validate_postcode"] = true
                })).GetProperty("object");
            }
            catch (VnptApiException ex)
            {
                _logger.LogWarning(ex, "VNPT OCR front side error");
                return Fail(SideMessage("front", ex));
            }

            try
            {
                backObj = (await PostAiAsync("/ai/v1/ocr/id/back", new()
                {
                    ["img_back"] = backHash,
                    ["type"] = -1
                })).GetProperty("object");
            }
            catch (VnptApiException ex)
            {
                _logger.LogWarning(ex, "VNPT OCR back side error");
                return Fail(SideMessage("back", ex));
            }

            var frontWarn = GetStringList(frontObj, "warning_msg");
            if (frontWarn.Count > 0)
                return Fail("The front side image does not meet quality standards: " + string.Join(", ", frontWarn));

            var backWarn = GetStringList(backObj, "warning_msg");
            if (backWarn.Count > 0)
                return Fail("The back side image does not meet quality standards: " + string.Join(", ", backWarn));
            var merged = new JsonObject();
            foreach (var p in frontObj.EnumerateObject()) merged[p.Name] = JsonNode.Parse(p.Value.GetRawText());
            foreach (var p in backObj.EnumerateObject()) merged[p.Name] = JsonNode.Parse(p.Value.GetRawText());
            var mergedJson = merged.ToJsonString();

            using var doc = JsonDocument.Parse(mergedJson);
            var obj = doc.RootElement;

            var error = ValidateOcr(obj);
            if (error is not null)
                return Fail(error);

            return new OcrResult
            {
                Success = true,
                IdNumber = GetString(obj, "id"),
                FullName = GetString(obj, "name"),
                DateOfBirth = GetString(obj, "birth_day"),
                RawResponseJson = "{\"object\":" + mergedJson + "}"
            };
        }

        private static string SideMessage(string side, Exception ex) =>
            ex is VnptApiException { Code: "IDG-00010003" }
                ? $"The {side} side image of the ID card does not meet quality standards (blurry, dark, glare, tilted or too small). Please retake the {side} side"
                : ToUserMessage(ex);

        // Returns an error message if the OCR result is not acceptable, null if it is OK
        private string? ValidateOcr(JsonElement obj)
        {
            if (GetString(obj, "msg") != "OK" || GetString(obj, "msg_back") != "OK")
                return "Unable to read the document information, please retake clear photos of both sides";

            // Image has issues: tilted, blurry, etc.
            var warnings = GetStringList(obj, "warning_msg");
            if (warnings.Count > 0)
                return "The document image does not meet quality standards: " + string.Join(", ", warnings);

            if (string.IsNullOrWhiteSpace(GetString(obj, "id")))
                return "Unable to read the ID number, please retake a clearer photo";

            // only a citizen ID card (CCCD, 12-digit number) is accepted
            var idNumber = (GetString(obj, "id") ?? string.Empty).Trim();
            if (idNumber.Length != 12 || !idNumber.All(char.IsDigit))
                return "Only 12-digit Citizen ID cards (CCCD) are accepted";

            // front and back must be the same card. The MRZ on the back embeds the full ID number.
            var mrz = GetString(obj, "mrz");
            if (!string.IsNullOrWhiteSpace(mrz)
                && mrz.Replace("<", "").Replace(" ", "").Length >= 20
                && !mrz.Contains(idNumber))
                return "The two sides do not belong to the same CCCD card";

            // The ID number must be valid according to the rules (tampering.is_legal = "yes")
            if (obj.TryGetProperty("tampering", out var tamper)
                && GetString(tamper, "is_legal") is { } legal
                && !legal.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return "The document number is invalid";

            if (string.Equals(GetString(obj, "id_fake_warning"), "yes", StringComparison.OrdinalIgnoreCase))
                return "The document number is suspected to be forged";

            // an expired ID card is always rejected (no longer controlled by configuration)
            if (string.Equals(GetString(obj, "expire_warning"), "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetString(obj, "back_expire_warning"), "yes", StringComparison.OrdinalIgnoreCase))
                return "The document has expired";

            return null;
        }

        // --------------------------------------------------------------
        // Face liveness (#8) + Face compare (#7)
        // --------------------------------------------------------------
        public async Task<FaceCompareResult> CompareFaceAsync(Stream idCardFront, Stream selfie)
        {
            try
            {
                var frontHash = await GetHashAsync(idCardFront, "front.jpg");
                var selfieHash = await GetHashAsync(selfie, "selfie.jpg");

                var live = (await PostAiAsync("/ai/v1/face/liveness",
                    new() { ["img"] = selfieHash })).GetProperty("object");
                var livenessPassed = IsSuccess(live, "liveness");

                // the face is compared with the ID card only after the liveness check has passed
                if (!livenessPassed)
                {
                    return new FaceCompareResult
                    {
                        Success = true,
                        MatchScore = 0m,
                        LivenessPassed = false,
                        LivenessScore = 0m
                    };
                }

                var cmp = (await PostAiAsync("/ai/v1/face/compare", new()
                {
                    ["img_front"] = frontHash,
                    ["img_face"] = selfieHash
                })).GetProperty("object");

                // The documentation says prob is a string but the example returns a number -> handle both
                TryGetDecimal(cmp, "prob", out var prob);

                return new FaceCompareResult
                {
                    Success = true,
                    MatchScore = prob,                                   // 0-100
                    LivenessPassed = livenessPassed,
                    LivenessScore = livenessPassed ? 100m : 0m           // VNPT only returns success/failure
                };
            }
            catch (Exception ex) when (ex is VnptApiException or HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "VNPT face compare error");
                return new FaceCompareResult { Success = false, ErrorMessage = ToUserMessage(ex) };
            }
        }

        // --------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------
        private static OcrResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        // Do not expose VNPT's error codes/details to the end user (details are in the log)
        private static string ToUserMessage(Exception ex) => ex switch
        {
            VnptApiException { Code: "IDG-00010003" } =>
                "Image quality does not meet standards (blurry, dark, glare, tilted or too small). Please retake a clear photo",
            VnptApiException { Code: "LOCAL-UNSUPPORTED-IMAGE" } =>
                "Only JPG or PNG images are supported",
            VnptApiException =>
                "Unable to verify the image, please retake it or try again later",
            TaskCanceledException =>
                "The verification system responded too slowly, please try again",
            _ =>
                "Unable to connect to the verification system, please try again later"
        };

        // Detect JPG/PNG by magic bytes (iPhone may send HEIC, Android may send WebP -> VNPT does not accept them)
        private static (string ContentType, string Extension)? DetectImageType(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
                return ("image/jpeg", ".jpg");
            if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                return ("image/png", ".png");
            return null;
        }

        // Upload image (#1) -> hash. Read into byte[] so HttpClient does not dispose KycService's stream.
        private async Task<string> GetHashAsync(Stream stream, string fileName)
        {
            var bytes = await ReadAllBytesAsync(stream);
            var cacheKey = Convert.ToHexString(SHA256.HashData(bytes));

            if (_hashCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var type = DetectImageType(bytes)
                ?? throw new VnptApiException("Unsupported image format (only JPG/PNG)", "LOCAL-UNSUPPORTED-IMAGE");

            var uploadName = Path.ChangeExtension(fileName, type.Extension);
            _logger.LogInformation("VNPT upload {File}: {SizeKb} KB, {Type}",
                uploadName, bytes.Length / 1024, type.ContentType);

            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(type.ContentType);
            form.Add(file, "file", uploadName);
            form.Add(new StringContent(uploadName), "title");
            form.Add(new StringContent(uploadName), "description");

            using var resp = await _http.PostAsync("/file-service/v1/addFile", form);
            var root = await ParseAsync(resp, "addFile");

            var hash = root.GetProperty("object").GetProperty("hash").GetString();
            if (string.IsNullOrEmpty(hash))
                throw new VnptApiException("addFile did not return a hash");

            _hashCache[cacheKey] = hash;
            return hash;
        }

        // Per the documentation: card/liveness and face/mask have no "token" field; the other APIs require it.
        private async Task<JsonElement> PostAiAsync(string path, Dictionary<string, object?> body, bool includeToken = true)
        {
            body["client_session"] = _clientSession;
            if (includeToken)
                body["token"] = Guid.NewGuid().ToString("N");   // any string, no special characters

            using var resp = await _http.PostAsJsonAsync(path, body);
            return await ParseAsync(resp, path);
        }

        // Success: message = "IDG-00000000".
        // Error: {status, message (e.g. IDG-00010102), statusCode, errors[]}
        private static async Task<JsonElement> ParseAsync(HttpResponseMessage resp, string path)
        {
            var json = await resp.Content.ReadAsStringAsync();

            JsonElement root = default;
            var parsed = false;
            try
            {
                using var doc = JsonDocument.Parse(json);
                root = doc.RootElement.Clone();
                parsed = true;
            }
            catch (JsonException) { }

            var code = parsed ? GetString(root, "message") : null;

            if (!resp.IsSuccessStatusCode || !parsed || code != OkCode)
            {
                var errors = parsed ? GetStringList(root, "errors") : new List<string>();
                throw new VnptApiException(
                    $"VNPT {path} error (HTTP {(int)resp.StatusCode}, {code ?? "n/a"})"
                    + (errors.Count > 0 ? ": " + string.Join("; ", errors) : ""),
                    code ?? $"HTTP{(int)resp.StatusCode}");
            }

            return root;
        }

        private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            if (stream.CanSeek) stream.Position = 0;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            if (stream.CanSeek) stream.Position = 0;
            return ms.ToArray();
        }

        private static string? GetString(JsonElement el, string name) =>
            el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static bool GetBool(JsonElement el, string name) =>
            el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.True;

        private static List<string> GetStringList(JsonElement el, string name)
        {
            var list = new List<string>();
            if (el.ValueKind == JsonValueKind.Object
                && el.TryGetProperty(name, out var arr)
                && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                        list.Add(s);
            }
            return list;
        }

        private static bool TryGetDecimal(JsonElement el, string name, out decimal value)
        {
            value = 0m;
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v))
                return false;

            return v.ValueKind switch
            {
                JsonValueKind.Number => v.TryGetDecimal(out value),
                JsonValueKind.String => decimal.TryParse(v.GetString(), NumberStyles.Float,
                                            CultureInfo.InvariantCulture, out value),
                _ => false
            };
        }

        private static bool IsSuccess(JsonElement el, string name) =>
            string.Equals(GetString(el, name), "success", StringComparison.OrdinalIgnoreCase);
    }
}