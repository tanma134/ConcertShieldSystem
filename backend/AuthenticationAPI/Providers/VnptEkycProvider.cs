using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuthenticationAPI.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Vi?t theo tài li?u "API eKYC" c?a VNPT (domain https://api.idg.vnpt.vn)
namespace AuthenticationAPI.Providers
{
    public class VnptEkycOptions
    {
        public string BaseUrl { get; set; } = "https://api.idg.vnpt.vn";
        public string AccessToken { get; set; } = "";
        public string TokenId { get; set; } = "";
        public string TokenKey { get; set; } = "";
        public string MacAddress { get; set; } = "AUTH-API-SERVER";

        // Tài li?u ðánh d?u crop_param là b?t bu?c cho /ai/v1/ocr/id
        // Ð?nh d?ng "<crop trên>,<crop dý?i>", ví d? "0.14,0.3"
        public string CropParam { get; set; } = "0.14,0.3";

        // Không c?n dùng:  luôn t? ch?i gi?y t? h?t h?n. Gi? l?i ð? appsettings c? không l?i.
        public bool RejectExpiredCard { get; set; } = true;

        // true: OCR t?ng m?t qua /ai/v1/ocr/id/front và /ai/v1/ocr/id/back (bi?t chính xác m?t nào l?i,
        // và không dùng crop_param). false: 1 l?n g?i /ai/v1/ocr/id cho c? 2 m?t.
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
                // Portal VNPT hi?n th? token d?ng "bearer eyJ..." -> b? ti?n t? ð? không thành "Bearer bearer eyJ..."
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

        // Tài li?u yêu c?u client_session theo cú pháp:
        // <IOS/ANDROID>_<model>_<OS/API>_<Device/Simulator>_<SDK version>_<Device id>_<Time stamp>
        // N?u app mobile g?i ðý?c session th?t lên backend th? nên dùng giá tr? ðó thay cho chu?i này.
        private readonly string _clientSession =
            $"ANDROID_backend_28_Device_1.0.0_{Guid.NewGuid():N}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        // Cache hash theo n?i dung ?nh: ?nh m?t trý?c dùng cho OCR + compare nhýng ch? upload 1 l?n
        private readonly Dictionary<string, string> _hashCache = new();

        public VnptEkycProvider(HttpClient http, IOptions<VnptEkycOptions> options, ILogger<VnptEkycProvider> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        // ---------------------------------------------------------------
        // Ki?m tra gi?y t? th?t/gi? (#3) + OCR (#6)
        // ---------------------------------------------------------------
        public async Task<OcrResult> ExtractIdCardInfoAsync(Stream front, Stream back)
        {
            try
            {
                _logger.LogInformation("VNPT OCR b?t ð?u: SplitOcr={Split}, CropParam='{Crop}'",
                    _options.SplitOcr, _options.CropParam);

                var frontHash = await GetHashAsync(front, "front.jpg");
                var backHash = await GetHashAsync(back, "back.jpg");

                // 1) /ai/v1/card/liveness (body theo tài li?u ch? có img + client_session)
                var cardLive = (await PostAiAsync("/ai/v1/card/liveness",
                    new() { ["img"] = frontHash }, includeToken: false)).GetProperty("object");

                if (!IsSuccess(cardLive, "liveness")
                    || GetBool(cardLive, "fake_liveness")     // gi?y t? b? ch?p l?i
                    || GetBool(cardLive, "face_swapping"))    // gi?y t? b? dán/thay ?nh
                {
                    return Fail("Gi?y t? không h?p l? ho?c nghi ng? gi? m?o"
                                + (GetString(cardLive, "liveness_msg") is { } m ? $" ({m})" : ""));
                }

                if (_options.SplitOcr)
                    return await ExtractSplitAsync(frontHash, backHash);

                // 2) /ai/v1/ocr/id
                var ocrBody = new Dictionary<string, object?>
                {
                    ["img_front"] = frontHash,
                    ["img_back"] = backHash,
                    ["type"] = -1,                       // CMT c?/m?i, CCCD
                    ["validate_postcode"] = true
                };
                // "0,0" = không crop. ?nh ð? ch?p sát gi?y t? th? KHÔNG nên crop m?nh (vd 0.14,0.3)
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
                _logger.LogError(ex, "VNPT OCR l?i");
                return Fail(ToUserMessage(ex));
            }
        }

        // OCR tách t?ng m?t: báo l?i chính xác m?t nào không ð?t
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
                _logger.LogWarning(ex, "VNPT OCR m?t trý?c l?i");
                return Fail(SideMessage("trý?c", ex));
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
                _logger.LogWarning(ex, "VNPT OCR m?t sau l?i");
                return Fail(SideMessage("sau", ex));
            }

            var frontWarn = GetStringList(frontObj, "warning_msg");
            if (frontWarn.Count > 0)
                return Fail("?nh m?t trý?c chýa ð?t: " + string.Join(", ", frontWarn));

            var backWarn = GetStringList(backObj, "warning_msg");
            if (backWarn.Count > 0)
                return Fail("?nh m?t sau chýa ð?t: " + string.Join(", ", backWarn));

            // G?p 2 object thành 1 ð? dùng l?i ValidateOcr (c?n c? msg và msg_back)
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
                ? $"?nh m?t {side} CCCD không ð?t chu?n (m?, t?i, chói sáng, m?t góc ho?c quá nh?). Vui l?ng ch?p l?i m?t {side}"
                : ToUserMessage(ex);

        // Tr? v? thông báo l?i n?u k?t qu? OCR không ð?t, null n?u ?n
        private string? ValidateOcr(JsonElement obj)
        {
            if (GetString(obj, "msg") != "OK" || GetString(obj, "msg_back") != "OK")
                return "Không ð?c ðý?c thông tin gi?y t?, vui l?ng ch?p l?i r? nét c? 2 m?t";

            // ?nh có v?n ð?: m?t góc, m?/nh?e...
            var warnings = GetStringList(obj, "warning_msg");
            if (warnings.Count > 0)
                return "?nh gi?y t? chýa ð?t: " + string.Join(", ", warnings);

            if (string.IsNullOrWhiteSpace(GetString(obj, "id")))
                return "Không ð?c ðý?c s? CCCD, vui l?ng ch?p l?i ?nh r? nét hõn";

            // only a citizen ID card (CCCD, 12-digit number) is accepted
            var idNumber = (GetString(obj, "id") ?? string.Empty).Trim();
            if (idNumber.Length != 12 || !idNumber.All(char.IsDigit))
                return "Ch? ch?p nh?n Cãn cý?c công dân (CCCD) 12 s?";

            // front and back must be the same card. The MRZ on the back embeds the full ID number.
            var mrz = GetString(obj, "mrz");
            if (!string.IsNullOrWhiteSpace(mrz)
                && mrz.Replace("<", "").Replace(" ", "").Length >= 20
                && !mrz.Contains(idNumber))
                return "Hai m?t gi?y t? không thu?c cùng m?t th? CCCD";

            // S? ID có h?p l? theo quy lu?t không (tampering.is_legal = "yes")
            if (obj.TryGetProperty("tampering", out var tamper)
                && GetString(tamper, "is_legal") is { } legal
                && !legal.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return "S? gi?y t? không h?p l?";

            if (string.Equals(GetString(obj, "id_fake_warning"), "yes", StringComparison.OrdinalIgnoreCase))
                return "S? gi?y t? nghi ng? gi? m?o";

            // an expired ID card is always rejected (no longer controlled by configuration)
            if (string.Equals(GetString(obj, "expire_warning"), "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetString(obj, "back_expire_warning"), "yes", StringComparison.OrdinalIgnoreCase))
                return "Gi?y t? ð? h?t h?n";

            return null;
        }

        // ---------------------------------------------------------------
        // Face liveness (#8) + Face compare (#7)
        // ---------------------------------------------------------------
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

                // Tài li?u ghi prob là string nhýng ví d? tr? s? -> x? l? c? hai
                TryGetDecimal(cmp, "prob", out var prob);

                return new FaceCompareResult
                {
                    Success = true,
                    MatchScore = prob,                                   // 0-100
                    LivenessPassed = livenessPassed,
                    LivenessScore = livenessPassed ? 100m : 0m           // VNPT ch? tr? success/failure
                };
            }
            catch (Exception ex) when (ex is VnptApiException or HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "VNPT face compare l?i");
                return new FaceCompareResult { Success = false, ErrorMessage = ToUserMessage(ex) };
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private static OcrResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        // Không ðýa m? l?i/chi ti?t c?a VNPT ra cho ngý?i dùng cu?i (chi ti?t ð? có trong log)
        private static string ToUserMessage(Exception ex) => ex switch
        {
            VnptApiException { Code: "IDG-00010003" } =>
                "Ch?t lý?ng ?nh không ð?t chu?n (m?, t?i, chói sáng, m?t góc ho?c quá nh?). Vui l?ng ch?p l?i r? nét",
            VnptApiException { Code: "LOCAL-UNSUPPORTED-IMAGE" } =>
                "Ch? h? tr? ?nh ð?nh d?ng JPG ho?c PNG",
            VnptApiException =>
                "Không xác th?c ðý?c ?nh, vui l?ng ch?p l?i ho?c th? l?i sau",
            TaskCanceledException =>
                "H? th?ng xác th?c ph?n h?i quá ch?m, vui l?ng th? l?i",
            _ =>
                "Không k?t n?i ðý?c h? th?ng xác th?c, vui l?ng th? l?i sau"
        };

        // Nh?n di?n JPG/PNG theo magic bytes (iPhone có th? g?i HEIC, Android có th? g?i WebP -> VNPT không nh?n)
        private static (string ContentType, string Extension)? DetectImageType(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
                return ("image/jpeg", ".jpg");
            if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                return ("image/png", ".png");
            return null;
        }

        // Upload ?nh (#1) -> hash. Ð?c ra byte[] ð? HttpClient không dispose stream c?a KycService.
        private async Task<string> GetHashAsync(Stream stream, string fileName)
        {
            var bytes = await ReadAllBytesAsync(stream);
            var cacheKey = Convert.ToHexString(SHA256.HashData(bytes));

            if (_hashCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var type = DetectImageType(bytes)
                ?? throw new VnptApiException("Ð?nh d?ng ?nh không ðý?c h? tr? (ch? JPG/PNG)", "LOCAL-UNSUPPORTED-IMAGE");

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
                throw new VnptApiException("addFile không tr? v? hash");

            _hashCache[cacheKey] = hash;
            return hash;
        }

        // Theo tài li?u: card/liveness và face/mask không có field "token"; các API c?n l?i có.
        private async Task<JsonElement> PostAiAsync(string path, Dictionary<string, object?> body, bool includeToken = true)
        {
            body["client_session"] = _clientSession;
            if (includeToken)
                body["token"] = Guid.NewGuid().ToString("N");   // chu?i b?t k?, không k? t? ð?c bi?t

            using var resp = await _http.PostAsJsonAsync(path, body);
            return await ParseAsync(resp, path);
        }

        // Thành công: message = "IDG-00000000".
        // L?i: {status, message (vd IDG-00010102), statusCode, errors[]}
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
                    $"VNPT {path} l?i (HTTP {(int)resp.StatusCode}, {code ?? "n/a"})"
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