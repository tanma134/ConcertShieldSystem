using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class VerifyStepUpOtpDTO
    {
        [Required(ErrorMessage = "OTP is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits.")]
        public string OTP { get; set; } = string.Empty;
    }

    public class StepUpTokenResponseDTO
    {
        public string StepUpToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
