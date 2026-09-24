using System.Text.Json;
using System.Text.Json.Nodes;

namespace AuthenticationAPI.Services
{
    /// <summary>
    /// Che số giấy tờ trong JSON OCR của VNPT trước khi lưu DB,
    /// để không lưu số CCCD ở dạng chữ rõ (số thật đã có ở cột mã hóa AES).
    /// </summary>
    public static class OcrDataSanitizer
    {
        // Che số giấy tờ
        private static readonly string[] SensitiveKeys = { "id", "citizen_id" };

        // Xóa hẳn: "mrz" là dải chữ ở mặt sau thẻ chứa NGUYÊN số CCCD + ngày sinh + họ tên;
        // "features" là đặc điểm nhận dạng cá nhân (sẹo, nốt ruồi...)
        private static readonly string[] RemovedKeys = { "mrz", "features" };

        public static string? Sanitize(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                return null;

            try
            {
                var root = JsonNode.Parse(rawJson);

                if (root?["object"] is JsonObject obj)
                {
                    foreach (var key in SensitiveKeys)
                    {
                        if (obj[key] is JsonValue v
                            && v.TryGetValue<string>(out var s)
                            && !string.IsNullOrWhiteSpace(s)
                            && s.Trim() != "-")
                        {
                            obj[key] = MaskId(s);
                        }
                    }
                }

                if (root?["object"] is JsonObject o)
                {
                    foreach (var key in RemovedKeys)
                        o.Remove(key);
                }

                return root?.ToJsonString();
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                // Không parse được thì thà không lưu còn hơn lưu nguyên bản chứa số CCCD
                return null;
            }
        }

        // 093204010643 -> 093*****0643
        private static string MaskId(string id)
        {
            var t = id.Trim();
            if (t.Length <= 6)
                return new string('*', t.Length);

            return t[..3] + new string('*', t.Length - 7) + t[^4..];
        }
    }
}