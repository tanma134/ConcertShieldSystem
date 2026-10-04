namespace AuthenticationAPI.DTOs
{
    public class EkycSubmitRequestDto
    {
        public IFormFile CccdFrontImage { get; set; } = null!;
        public IFormFile CccdBackImage { get; set; } = null!;
        public IFormFile SelfieImage { get; set; } = null!;

        /// <summary>Người dùng đã tích đồng ý (mặc định false - không được đồng ý sẵn).</summary>
        public bool ConsentAccepted { get; set; }

        /// <summary>Phiên bản nội dung đồng ý mà người dùng đã thấy (lấy từ GET api/kyc/consent).</summary>
        public string? ConsentVersion { get; set; }
    }

    public class EkycSubmitResponseDto
    {
        public int EkycId { get; set; }
        public string Status { get; set; } = null!;
        public string? Message { get; set; }

        /// <summary>BR-44: identity read from the ID card, shown to the user for review (read-only, ID number masked).</summary>
        public EkycIdentityInfoDto? Identity { get; set; }
    }

    public class EkycIdentityInfoDto
    {
        public string? FullName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? IdNumberMasked { get; set; }
    }

    public class EkycStatusResponseDto
    {
        public int EkycId { get; set; }
        public string Status { get; set; } = null!;
        public decimal? FaceMatchScore { get; set; }
        public string? FailReason { get; set; }
        public string? VerifiedToken { get; set; }
    }
}