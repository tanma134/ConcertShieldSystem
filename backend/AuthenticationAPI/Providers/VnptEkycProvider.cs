using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuthenticationAPI.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Viết theo tài liệu "API eKYC" của VNPT (domain https://api.idg.vnpt.vn)
namespace AuthenticationAPI.Providers
{
    public class VnptEkycOptions
    {
        public string BaseUrl { get; set; } = "https://api.idg.vnpt.vn";
        public string AccessToken { get; set; } = "";
        public string TokenId { get; set; } = "";
        public string TokenKey { get; set; } = "";
        public string MacAddress { get; set; } = "AUTH-API-SERVER";

        // Tài liệu đánh dấu crop_param là bắt buộc cho /ai/v1/ocr/id
        // Định dạng "<crop trên>,<crop dưới>", ví dụ "0.14,0.3"
        public string CropParam { get; set; } = "0.14,0.3";

        // Từ chối giấy tờ đã hết hạn (expire_warning / back_expire_warning)
        public bool RejectExpiredCard { get; set; } = true;

        // true: OCR từng mặt qua /ai/v1/ocr/id/front và /ai/v1/ocr/id/back (biết chính xác mặt nào lỗi,
        // và không dùng crop_param). false: 1 lần gọi /ai/v1/ocr/id cho cả 2 mặt.
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
                // Portal VNPT hiển thị token dạng "bearer eyJ..." -> bỏ tiền tố để không thành "Bearer bearer eyJ..."
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

        // Tài liệu yêu cầu client_session theo cú pháp:
        // <IOS/ANDROID>_<model>_<OS/API>_<Device/Simulator>_<SDK version>_<Device id>_<Time stamp>
        // Nếu app mobile gửi được session thật lên backend thì nên dùng giá trị đó thay cho chuỗi này.
        private readonly string _clientSession =
            $"ANDROID_backend_28_Device_1.0.0_{Guid.NewGuid():N}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        // Cache hash theo nội dung ảnh: ảnh mặt trước dùng cho OCR + compare nhưng chỉ upload 1 lần
        private readonly Dictionary<string, string> _hashCache = new();

        public VnptEkycProvider(HttpClient http, IOptions<VnptEkycOptions> options, ILogger<VnptEkycProvider> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        // ---------------------------------------------------------------
        // Kiểm tra giấy tờ thật/giả (#3) + OCR (#6)
        // ---------------------------------------------------------------
        public async Task<OcrResult> ExtractIdCardInfoAsync(Stream front, Stream back)
        {
            try
            {
                _logger.LogInformation("VNPT OCR bắt đầu: SplitOcr={Split}, CropParam='{Crop}'",
                    _options.SplitOcr, _options.CropParam);

                var frontHash = await GetHashAsync(front, "front.jpg");
                var backHash = await GetHashAsync(back, "back.jpg");

                // 1) /ai/v1/card/liveness (body theo tài liệu chỉ có img + client_session)
                var cardLive = (await PostAiAsync("/ai/v1/card/liveness",
                    new() { ["img"] = frontHash }, includeToken: false)).GetProperty("object");

                if (!IsSuccess(cardLive, "liveness")
                    || GetBool(cardLive, "fake_liveness")     // giấy tờ bị chụp lại
                    || GetBool(cardLive, "face_swapping"))    // giấy tờ bị dán/thay ảnh
                {
                    return Fail("Giấy tờ không hợp lệ hoặc nghi ngờ giả mạo"
                                + (GetString(cardLive, "liveness_msg") is { } m ? $" ({m})" : ""));
                }

                if (_options.SplitOcr)
                    return await ExtractSplitAsync(frontHash, backHash);

                // 2) /ai/v1/ocr/id
                var ocrBody = new Dictionary<string, object?>
                {
                    ["img_front"] = frontHash,
                    ["img_back"] = backHash,
                    ["type"] = -1,                       // CMT cũ/mới, CCCD
                    ["validate_postcode"] = true
                };
                // "0,0" = không crop. Ảnh đã chụp sát giấy tờ thì KHÔNG nên crop mạnh (vd 0.14,0.3)
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
                    RawResponseJson = ocr.GetRawText()
                };
            }
            catch (Exception ex) when (ex is VnptApiException or HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "VNPT OCR lỗi");
                return Fail(ToUserMessage(ex));
            }
        }

        // OCR tách từng mặt: báo lỗi chính xác mặt nào không đạt
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
                _logger.LogWarning(ex, "VNPT OCR mặt trước lỗi");
                return Fail(SideMessage("trước", ex));
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
                _logger.LogWarning(ex, "VNPT OCR mặt sau lỗi");
                return Fail(SideMessage("sau", ex));
            }

            var frontWarn = GetStringList(frontObj, "warning_msg");
            if (frontWarn.Count > 0)
                return Fail("Ảnh mặt trước chưa đạt: " + string.Join(", ", frontWarn));

            var backWarn = GetStringList(backObj, "warning_msg");
            if (backWarn.Count > 0)
                return Fail("Ảnh mặt sau chưa đạt: " + string.Join(", ", backWarn));

            // Gộp 2 object thành 1 để dùng lại ValidateOcr (cần cả msg và msg_back)
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
                RawResponseJson = "{\"object\":" + mergedJson + "}"
            };
        }

        private static string SideMessage(string side, Exception ex) =>
            ex is VnptApiException { Code: "IDG-00010003" }
                ? $"Ảnh mặt {side} CCCD không đạt chuẩn (mờ, tối, chói sáng, mất góc hoặc quá nhỏ). Vui lòng chụp lại mặt {side}"
                : ToUserMessage(ex);

        // Trả về thông báo lỗi nếu kết quả OCR không đạt, null nếu ổn
        private string? ValidateOcr(JsonElement obj)
        {
            if (GetString(obj, "msg") != "OK" || GetString(obj, "msg_back") != "OK")
                return "Không đọc được thông tin giấy tờ, vui lòng chụp lại rõ nét cả 2 mặt";

            // Ảnh có vấn đề: mất góc, mờ/nhòe...
            var warnings = GetStringList(obj, "warning_msg");
            if (warnings.Count > 0)
                return "Ảnh giấy tờ chưa đạt: " + string.Join(", ", warnings);

            if (string.IsNullOrWhiteSpace(GetString(obj, "id")))
                return "Không đọc được số CCCD, vui lòng chụp lại ảnh rõ nét hơn";

            // Số ID có hợp lệ theo quy luật không (tampering.is_legal = "yes")
            if (obj.TryGetProperty("tampering", out var tamper)
                && GetString(tamper, "is_legal") is { } legal
                && !legal.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return "Số giấy tờ không hợp lệ";

            if (string.Equals(GetString(obj, "id_fake_warning"), "yes", StringComparison.OrdinalIgnoreCase))
                return "Số giấy tờ nghi ngờ giả mạo";

            if (_options.RejectExpiredCard
                && (string.Equals(GetString(obj, "expire_warning"), "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(GetString(obj, "back_expire_warning"), "yes", StringComparison.OrdinalIgnoreCase)))
                return "Giấy tờ đã hết hạn";

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

                var cmp = (await PostAiAsync("/ai/v1/face/compare", new()
                {
                    ["img_front"] = frontHash,
                    ["img_face"] = selfieHash
                })).GetProperty("object");

                // Tài liệu ghi prob là string nhưng ví dụ trả số -> xử lý cả hai
                TryGetDecimal(cmp, "prob", out var prob);

                return new FaceCompareResult
                {
                    Success = true,
                    MatchScore = prob,                                   // 0-100
                    LivenessPassed = livenessPassed,
                    LivenessScore = livenessPassed ? 100m : 0m           // VNPT chỉ trả success/failure
                };
            }
            catch (Exception ex) when (ex is VnptApiException or HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "VNPT face compare lỗi");
                return new FaceCompareResult { Success = false, ErrorMessage = ToUserMessage(ex) };
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private static OcrResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };

        // Không đưa mã lỗi/chi tiết của VNPT ra cho người dùng cuối (chi tiết đã có trong log)
        private static string ToUserMessage(Exception ex) => ex switch
        {
            VnptApiException { Code: "IDG-00010003" } =>
                "Chất lượng ảnh không đạt chuẩn (mờ, tối, chói sáng, mất góc hoặc quá nhỏ). Vui lòng chụp lại rõ nét",
            VnptApiException { Code: "LOCAL-UNSUPPORTED-IMAGE" } =>
                "Chỉ hỗ trợ ảnh định dạng JPG hoặc PNG",
            VnptApiException =>
                "Không xác thực được ảnh, vui lòng chụp lại hoặc thử lại sau",
            TaskCanceledException =>
                "Hệ thống xác thực phản hồi quá chậm, vui lòng thử lại",
            _ =>
                "Không kết nối được hệ thống xác thực, vui lòng thử lại sau"
        };

        // Nhận diện JPG/PNG theo magic bytes (iPhone có thể gửi HEIC, Android có thể gửi WebP -> VNPT không nhận)
        private static (string ContentType, string Extension)? DetectImageType(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
                return ("image/jpeg", ".jpg");
            if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                return ("image/png", ".png");
            return null;
        }

        // Upload ảnh (#1) -> hash. Đọc ra byte[] để HttpClient không dispose stream của KycService.
        private async Task<string> GetHashAsync(Stream stream, string fileName)
        {
            var bytes = await ReadAllBytesAsync(stream);
            var cacheKey = Convert.ToHexString(SHA256.HashData(bytes));

            if (_hashCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var type = DetectImageType(bytes)
                ?? throw new VnptApiException("Định dạng ảnh không được hỗ trợ (chỉ JPG/PNG)", "LOCAL-UNSUPPORTED-IMAGE");

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
                throw new VnptApiException("addFile không trả về hash");

            _hashCache[cacheKey] = hash;
            return hash;
        }

        // Theo tài liệu: card/liveness và face/mask không có field "token"; các API còn lại có.
        private async Task<JsonElement> PostAiAsync(string path, Dictionary<string, object?> body, bool includeToken = true)
        {
            body["client_session"] = _clientSession;
            if (includeToken)
                body["token"] = Guid.NewGuid().ToString("N");   // chuỗi bất kỳ, không ký tự đặc biệt

            using var resp = await _http.PostAsJsonAsync(path, body);
            return await ParseAsync(resp, path);
        }

        // Thành công: message = "IDG-00000000".
        // Lỗi: {status, message (vd IDG-00010102), statusCode, errors[]}
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
                    $"VNPT {path} lỗi (HTTP {(int)resp.StatusCode}, {code ?? "n/a"})"
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