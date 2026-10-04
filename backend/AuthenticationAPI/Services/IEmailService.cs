namespace AuthenticationAPI.Services
{
    public enum EmailPurpose
    {
        Register,
        ForgotPassword
    }

    public interface IEmailService
    {
        Task SendOTPAsync(string toEmail, string otp, EmailPurpose purpose);

        Task SendKycDeletionResultAsync(string toEmail, bool approved, string? reason);
    }
}