namespace AuthenticationAPI.Providers
{
    public interface IEkycProvider
    {
        /// <summary>Kiểm tra giấy tờ thật/giả + OCR 2 mặt CCCD.</summary>
        Task<OcrResult> ExtractIdCardInfoAsync(Stream front, Stream back);

        /// <summary>Face liveness cho selfie + so khớp mặt trên CCCD với selfie.</summary>
        Task<FaceCompareResult> CompareFaceAsync(Stream idCardFront, Stream selfie);
    }

    public class OcrResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? IdNumber { get; set; }
        public string? RawResponseJson { get; set; }
    }

    public class FaceCompareResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal MatchScore { get; set; }      // 0-100
        public decimal LivenessScore { get; set; }
        public bool LivenessPassed { get; set; }
    }
}