using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AuthenticationAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOTPAsync(string toEmail, string otp, EmailPurpose purpose)
        {
            var settings = _configuration.GetSection("EmailSettings");
            var host = settings["Host"]!;
            var port = int.Parse(settings["Port"]!);
            var enableSsl = bool.Parse(settings["EnableSsl"]!);
            var senderEmail = settings["SenderEmail"]!;
            var senderName = settings["SenderName"]!;
            var password = settings["Password"]!;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));

            message.Subject = purpose switch
            {
                EmailPurpose.Register => "Your OTP Verification Code",
                EmailPurpose.ForgotPassword => "Reset Your Password – OTP Code",
                _ => "OTP Code"
            };

            message.Body = new TextPart("html")
            {
                Text = purpose switch
                {
                    EmailPurpose.Register => BuildRegisterTemplate(otp),
                    EmailPurpose.ForgotPassword => BuildForgotPasswordTemplate(otp),
                    _ => BuildRegisterTemplate(otp)
                }
            };

            using var client = new SmtpClient();
            var secureOption = enableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(host, port, secureOption);
            await client.AuthenticateAsync(senderEmail, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
        }

        // ─── Email Templates ─────────────────────────────────────────────────

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