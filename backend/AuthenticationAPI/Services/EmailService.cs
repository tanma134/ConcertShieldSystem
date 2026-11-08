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

        public async Task SendKycDeletionResultAsync(string toEmail, bool approved, string? reason)
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
            message.Subject = approved
                ? "Your eKYC data deletion request was approved"
                : "Your eKYC data deletion request was rejected";

            var safeReason = System.Net.WebUtility.HtmlEncode(reason ?? "");
            message.Body = new TextPart("html")
            {
                Text = approved
                    ? @"<div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                            <h2 style='color: #333;'>Deletion request approved</h2>
                            <p>Your ID card and face data has been deleted from our system.</p>
                        </div>"
                    : $@"<div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                            <h2 style='color: #333;'>Deletion request rejected</h2>
                            <p>We could not delete your eKYC data for the following reason:</p>
                            <p><strong>{safeReason}</strong></p>
                        </div>"
            };

            using var client = new SmtpClient();
            var secureOption = enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(host, port, secureOption);
            await client.AuthenticateAsync(senderEmail, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
        }

        public async Task SendEventReminderAsync(
            string toEmail,
            string recipientName,
            string eventTitle,
            string eventSlug,
            DateTime startsAt,
            string timezone,
            CancellationToken cancellationToken)
        {
            var settings = _configuration.GetSection("EmailSettings");
            var host = settings["Host"]!;
            var port = int.Parse(settings["Port"]!);
            var enableSsl = bool.Parse(settings["EnableSsl"]!);
            var senderEmail = settings["SenderEmail"]!;
            var senderName = settings["SenderName"]!;
            var password = settings["Password"]!;
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
            var eventUrl = $"{frontendBaseUrl.TrimEnd('/')}/events/{Uri.EscapeDataString(eventSlug)}";
            var safeName = System.Net.WebUtility.HtmlEncode(recipientName);
            var safeTitle = System.Net.WebUtility.HtmlEncode(eventTitle);
            var safeTimezone = System.Net.WebUtility.HtmlEncode(timezone);
            var safeEventUrl = System.Net.WebUtility.HtmlEncode(eventUrl);
            var localStartsAt = FormatEventStart(startsAt, timezone);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"Nhắc nhở: {eventTitle} sẽ diễn ra sau 1 ngày";
            message.Body = new TextPart("html")
            {
                Text = $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; color: #333;'>
                    <h2>Sự kiện bạn yêu thích sắp diễn ra</h2>
                    <p>Xin chào {safeName},</p>
                    <p>Sự kiện <strong>{safeTitle}</strong> trong wishlist của bạn sẽ bắt đầu sau khoảng 1 ngày.</p>
                    <p>Thời gian: <strong>{localStartsAt} ({safeTimezone})</strong></p>
                    <p><a href='{safeEventUrl}'>Xem thông tin sự kiện</a></p>
                </div>"
            };

            using var client = new SmtpClient();
            var secureOption = enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(host, port, secureOption, cancellationToken);
            await client.AuthenticateAsync(senderEmail, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }

        private static string FormatEventStart(DateTime startsAt, string timezone)
        {
            var utcStartsAt = DateTime.SpecifyKind(startsAt, DateTimeKind.Utc);
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                return TimeZoneInfo.ConvertTimeFromUtc(utcStartsAt, timeZone).ToString("dd/MM/yyyy HH:mm");
            }
            catch (TimeZoneNotFoundException)
            {
                return utcStartsAt.ToString("dd/MM/yyyy HH:mm 'UTC'");
            }
            catch (InvalidTimeZoneException)
            {
                return utcStartsAt.ToString("dd/MM/yyyy HH:mm 'UTC'");
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