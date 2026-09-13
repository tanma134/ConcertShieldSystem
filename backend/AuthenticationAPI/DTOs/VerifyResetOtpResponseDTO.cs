namespace AuthenticationAPI.DTOs
{
    public class VerifyResetOtpResponseDTO
    {
        public string ResetToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
