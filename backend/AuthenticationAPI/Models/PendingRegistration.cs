namespace AuthenticationAPI.Models
{
    public class PendingRegistration
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string PasswordHash { get; set; } = null!;
        public string OtpCode { get; set; } = null!;
        public DateTime ExpiredAt { get; set; }

        /// <summary>Wrong OTP entries for this pending registration (brute-force guard).</summary>
        public int FailedOtpAttempts { get; set; }
    }
}
