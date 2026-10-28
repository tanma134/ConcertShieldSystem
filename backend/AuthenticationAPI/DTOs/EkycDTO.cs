namespace AuthenticationAPI.DTOs
{
    public class EkycSubmitRequestDto
    {
        public IFormFile CccdFrontImage { get; set; } = null!;
        public IFormFile CccdBackImage { get; set; } = null!;
        public IFormFile SelfieImage { get; set; } = null!;

        /// <summary>Ngý?i dùng ð? tích ð?ng ? (m?c ð?nh false - không ðý?c ð?ng ? s?n).</summary>
        public bool ConsentAccepted { get; set; }

        /// <summary>Phiên b?n n?i dung ð?ng ? mà ngý?i dùng ð? th?y (l?y t? GET api/kyc/consent).</summary>
        public string? ConsentVersion { get; set; }
    }

    public class EkycSubmitResponseDto
    {
        public int EkycId { get; set; }
        public string Status { get; set; } = null!;
        public string? Message { get; set; }

        /// <summary> identity read from the ID card, shown to the user for review (read-only, ID number masked).</summary>
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