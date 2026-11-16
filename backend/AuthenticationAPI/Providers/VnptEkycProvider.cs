using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuthenticationAPI.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Vi?t theo ti li?u "API eKYC" c?a VNPT (domain https://api.idg.vnpt.vn)
namespace AuthenticationAPI.Providers
{
    public class VnptEkycOptions
    {
        public string BaseUrl { get; set; } = "https://api.idg.vnpt.vn";
        public string AccessToken { get; set; } = "";
        public string TokenId { get; set; } = "";
        public string TokenKey { get; set; } = "";
        public string MacAddress { get; set; } = "AUTH-API-SERVER";

        // Ti li?u nh d?u crop_param l b?t bu?c cho /ai/v1/ocr/id
        // ?nh d?ng "<crop trn>,<crop d?i>", v d? "0.14,0.3"
        public string CropParam { get; set; } = "0.14,0.3";

        // Khng c?n dng:  lun t? ch?i gi?y t? h?t h?n. Gi? l?i ? appsettings c? khng l?i.
        public bool RejectExpiredCard { get; set; } = true;

        // true: OCR t?ng m?t qua /ai/v1/ocr/id/front v /ai/v1/ocr/id/back (bi?t chnh xc m?t no l?i,
        // v khng dng crop_param). false: 1 l?n g?i /ai/v1/ocr/id cho c? 2 m?t.
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
                // Portal VNPT hi?n th? token d?ng "bearer eyJ..." -> b? ti?n t? ? khng thnh "Bearer bearer eyJ..."
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

        // Ti li?u yu c?u client_session theo c php:
        // <IOS/ANDROID>_<model>_<OS/API>_<Device/Simulator>_<SDK version>_<Device id>_<Time stamp>
        // N?u app mobile g?i ?c session th?t ln backend th? nn dng gi tr?  thay cho chu?i ny.
        private readonly string _clientSession =
            $"ANDROID_backend_28_Device_1.0.0_{Guid.NewGuid():N}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        // Cache hash theo n?i dung ?nh: ?nh m?t tr?c dng cho OCR + compare nhng ch? upload 1 l?n
        private readonly Dictionary<string, string> _hashCache = new();

        public VnptEkycProvider(HttpClient http, IOptions<VnptEkycOptions> options, ILogger<VnptEkycProvider> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        // --------------------------------------------------------------
        // Ki?m tra gi?y t? th?t/gi? (#3) + OCR (#6)
        // --------------------------------------------------------------
        public async Task<OcrResult> ExtractIdCardInfoAsync(Stream front, Stream back)
        {
            try
            {
                _logger.LogInformation("VNPT OCR b?t ?u: SplitOcr={Split}, CropParam='{Crop}'",
                    _options.SplitOcr, _options.CropParam);

                var frontHash = await GetHashAsync(front, "front.jpg");
                var backHash = await GetHashAsync(back, "back.jpg");

                // 1) /ai/v1/card/liveness (body theo ti li?u ch? c img + client_session)
                var cardLive = (await PostAiAsync("/ai/v1/card/liveness",
                    new() { ["img"] = frontHash }, includeToken: false)).GetProperty("object");

                if (!IsSuccess(cardLive, "liveness")
                    || GetBool(cardLive, "fake_liveness")     // gi?y t? b? ch?p l?i
                    || GetBool(cardLive, "face_swapping"))    // gi?y t? b? dn/thay ?nh
                {
                    return Fail("Gi?y t? khng h?p l? ho?c nghi ng? gi? m?o"
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
                // "0,0" = khng crop. ?nh ? ch?p st gi?y t? th? KHNG nn crop m?nh (vd 0.14,0.3)
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

        // OCR tch t?ng m?t: bo l?i chnh xc m?t no khng ?t
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
                _logger.LogWarning(ex, "VNPT OCR m?t tr?c l?i");
                return Fail(SideMessage("tr?c", ex));
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
                return Fail("?nh m?t tr?c cha ?t: " + string.Join(", ", frontWarn));

            var backWarn = GetStringList(backObj, "warning_msg");
            if (backWarn.Count > 0)
                return Fail("?nh m?t sau cha ?t: " + string.Join(", ", backWarn));

            // G?p 2 object thnh 1 ? dng l?i ValidateOcr (c?n c? msg v msg_back)
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
                ? $"?nh m?t {side} CCCD khng ?t chu?n (m?, t?i, chi sng, m?t gc ho?c qu nh?). Vui l?ng ch?p l?i m?t {side}"
                : ToUserMessage(ex);

        // Tr? v? thng bo l?i n?u k?t qu? OCR khng ?t, null n?u ?n
        private string? ValidateOcr(JsonElement obj)
        {
            if (GetString(obj, "msg") != "OK" || GetString(obj, "msg_back") != "OK")
                return "Khng ?c ?c thng tin gi?y t?, vui l?ng ch?p l?i r? nt c? 2 m?t";

            // ?nh c v?n ?: m?t gc, m?/nh?e...
            var warnings = GetStringList(obj, "warning_msg");
            if (warnings.Count > 0)
                return "?nh gi?y t? cha ?t: " + string.Join(", ", warnings);

            if (string.IsNullOrWhiteSpace(GetString(obj, "id")))
                return "Khng ?c ?c s? CCCD, vui l?ng ch?p l?i ?nh r? nt hn";

            // only a citizen ID card (CCCD, 12-digit number) is accepted
            var idNumber = (GetString(obj, "id") ?? string.Empty).Trim();
            if (idNumber.Length != 12 || !idNumber.All(char.IsDigit))
                return "Ch? ch?p nh?n Cn c?c cng dn (CCCD) 12 s?";

            // front and back must be the same card. The MRZ on the back embeds the full ID number.
            var mrz = GetString(obj, "mrz");
            if (!string.IsNullOrWhiteSpace(mrz)
                && mrz.Replace("<", "").Replace(" ", "").Length >= 20
                && !mrz.Contains(idNumber))
                return "Hai m?t gi?y t? khng thu?c cng m?t th? CCCD";

            // S? ID c h?p l? theo quy lu?t khng (tampering.is_legal = "yes")
            if (obj.TryGetProperty("tampering", out var tamper)
                && GetString(tamper, "is_legal") is { } legal
                && !legal.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return "S? gi?y t? khng h?p l?";

            if (string.Equals(GetString(obj, "id_fake_warning"), "yes", StringComparison.OrdinalIgnoreCase))
                return "S? gi?y t? nghi ng? gi? m?o";

            // an expired ID card is always rejected (no longer controlled by configuration)
            if (string.Equals(GetString(obj, "expire_warning"), "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetString(obj, "back_expire_warning"), "yes", StringComparison.OrdinalIgnoreCase))
                return "Gi?y t? ? h?t h?n";

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

                // Ti li?u ghi prob l string nhng v d? tr? s? -> x? l? c? hai
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

        // --------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------
        private static OcrResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        // Khng a m? l?i/chi ti?t c?a VNPT ra cho ng?i dng cu?i (chi ti?t ? c trong log)
        private static string ToUserMessage(Exception ex) => ex switch
        {
            VnptApiException { Code: "IDG-00010003" } =>
                "Ch?t l?ng ?nh khng ?t chu?n (m?, t?i, chi sng, m?t gc ho?c qu nh?). Vui l?ng ch?p l?i r? nt",
            VnptApiException { Code: "LOCAL-UNSUPPORTED-IMAGE" } =>
                "Ch? h? tr? ?nh ?nh d?ng JPG ho?c PNG",
            VnptApiException =>
                "Khng xc th?c ?c ?nh, vui l?ng ch?p l?i ho?c th? l?i sau",
            TaskCanceledException =>
                "H? th?ng xc th?c ph?n h?i qu ch?m, vui l?ng th? l?i",
            _ =>
                "Khng k?t n?i ?c h? th?ng xc th?c, vui l?ng th? l?i sau"
        };

        // Nh?n di?n JPG/PNG theo magic bytes (iPhone c th? g?i HEIC, Android c th? g?i WebP -> VNPT khng nh?n)
        private static (string ContentType, string Extension)? DetectImageType(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
                return ("image/jpeg", ".jpg");
            if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                return ("image/png", ".png");
            return null;
        }

        // Upload ?nh (#1) -> hash. ?c ra byte[] ? HttpClient khng dispose stream c?a KycService.
        private async Task<string> GetHashAsync(Stream stream, string fileName)
        {
            var bytes = await ReadAllBytesAsync(stream);
            var cacheKey = Convert.ToHexString(SHA256.HashData(bytes));

            if (_hashCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var type = DetectImageType(bytes)
                ?? throw new VnptApiException("?nh d?ng ?nh khng ?c h? tr? (ch? JPG/PNG)", "LOCAL-UNSUPPORTED-IMAGE");

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
                throw new VnptApiException("addFile khng tr? v? hash");

            _hashCache[cacheKey] = hash;
            return hash;
        }

        // Theo ti li?u: card/liveness v face/mask khng c field "token"; cc API c?n l?i c.
        private async Task<JsonElement> PostAiAsync(string path, Dictionary<string, object?> body, bool includeToken = true)
        {
            body["client_session"] = _clientSession;
            if (includeToken)
                body["token"] = Guid.NewGuid().ToString("N");   // chu?i b?t k?, khng k? t? ?c bi?t

            using var resp = await _http.PostAsJsonAsync(path, body);
            return await ParseAsync(resp, path);
        }

        // Thnh cng: message = "IDG-00000000".
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