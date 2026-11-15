using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AuthenticationAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendOTPAsync(string toEmail, string otp, EmailPurpose purpose)
        {
            var subject = purpose switch
            {
                EmailPurpose.Register => "Your OTP Verification Code",
                EmailPurpose.ForgotPassword => "Reset Your Password – OTP Code",
                _ => "OTP Code"
            };

            var body = purpose switch
            {
                EmailPurpose.Register => BuildRegisterTemplate(otp),
                EmailPurpose.ForgotPassword => BuildForgotPasswordTemplate(otp),
                _ => BuildRegisterTemplate(otp)
            };

            await SendAsync(toEmail, subject, body);
        }

        public async Task SendKycDeletionResultAsync(string toEmail, bool approved, string? reason)
        {
            var subject = approved
                ? "Your eKYC data deletion request was approved"
                : "Your eKYC data deletion request was rejected";

            var safeReason = System.Net.WebUtility.HtmlEncode(reason ?? "");
            var body = approved
                ? @"<div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                        <h2 style='color: #333;'>Deletion request approved</h2>
                        <p>Your ID card and face data has been deleted from our system.</p>
                    </div>"
                : $@"<div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                        <h2 style='color: #333;'>Deletion request rejected</h2>
                        <p>We could not delete your eKYC data for the following reason:</p>
                        <p><strong>{safeReason}</strong></p>
                    </div>";

            await SendAsync(toEmail, subject, body);
        }

        public async Task SendKycReviewResultAsync(string toEmail, bool approved, string? reason)
        {
            var subject = approved
                ? "Your eKYC verification was approved"
                : "Your eKYC verification could not be approved";

            var safeReason = System.Net.WebUtility.HtmlEncode(reason ?? "");
            var body = approved
                ? @"<div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                        <h2 style='color: #333;'>Verification approved</h2>
                        <p>Your identity verification has been reviewed and approved.</p>
                    </div>"
                : $@"<div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                        <h2 style='color: #333;'>Verification not approved</h2>
                        <p>Our team reviewed your verification and could not approve it:</p>
                        <p><strong>{safeReason}</strong></p>
                        <p>You may submit a new verification request with clearer photos.</p>
                    </div>";

            await SendAsync(toEmail, subject, body);
        }

        // Shared SMTP logic: config is read in ONE place only.
        private async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var settings = _configuration.GetSection("EmailSettings");
            var host = settings["Host"]!;
            var port = int.Parse(settings["Port"]!);
            var enableSsl = bool.Parse(settings["EnableSsl"]!);
            var senderEmail = settings["SenderEmail"]!;
            var senderName = settings["SenderName"]!;
            var password = settings["Password"]!;

            // DEBUG: shows which config source the app actually reads (never logs the password itself).
            LogConfigSource();
            _logger.LogInformation(
                "[EMAIL] host={Host}:{Port}, ssl={Ssl}, sender={Sender}, pwdLen={PwdLen}",
                host, port, enableSsl, senderEmail, password.Length);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            var secureOption = enableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(host, port, secureOption);
                await client.AuthenticateAsync(senderEmail, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(quit: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail} via {Host}:{Port} as {Sender}",
                    toEmail, host, port, senderEmail);
                throw new InvalidOperationException("Unable to send email. Please check the email settings.", ex);
            }
        }

        private void LogConfigSource()
        {
            if (_configuration is not IConfigurationRoot root) return;

            foreach (var key in new[] { "EmailSettings:SenderEmail", "EmailSettings:Password" })
            {
                var provider = root.Providers.Reverse().FirstOrDefault(p => p.TryGet(key, out _));
                _logger.LogInformation("[EMAIL] {Key} is read from: {Provider}", key, provider?.ToString() ?? "(not found)");
            }
        }

        private static string BuildRegisterTemplate(string otp) => $@"
            <div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                <h2 style='color: #333;'>Email Verification</h2>
                <p>Thank you for registering. Use the OTP below to verify your account:</p>
                {OtpBox(otp)}
                <p style='color: #6B7280; font-size: 13px;'>
                    This OTP is valid for <strong>5 minutes</strong>.
                    Do not share it with anyone.
                </p>
            </div>";

        private static string BuildForgotPasswordTemplate(string otp) => $@"
            <div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                <h2 style='color: #333;'>Reset Your Password</h2>
                <p>We received a request to reset the password for your account.</p>
                <p>Use the OTP below to set a new password:</p>
                {OtpBox(otp)}
                <p style='color: #6B7280; font-size: 13px;'>
                    This OTP is valid for <strong>5 minutes</strong>.
                    If you did not request a password reset, please ignore this email —
                    your password will remain unchanged.
                </p>
            </div>";

        private static string OtpBox(string otp) => $@"
            <div style='
                font-size: 36px;
                font-weight: bold;
                letter-spacing: 8px;
                color: #4F46E5;
                padding: 16px;
                background: #F3F4F6;
                text-align: center;
                border-radius: 8px;
                margin: 24px 0;
            '>{otp}</div>";
    }
}