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

        Task SendEventReminderAsync(
            string toEmail,
            string recipientName,
            string eventTitle,
            string eventSlug,
            DateTime startsAt,
            string timezone,
            CancellationToken cancellationToken);
    }
}