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
                 <h2>Your Favorite Event Is Coming Soon!</h2>
                <p>Hello {safeName},</p>
                 <p>The event <strong>{safeTitle}</strong> in your wishlist is scheduled to start in approximately 1 day.</p>
                 <p>Event Time: <strong>{localStartsAt} ({safeTimezone})</strong></p>
                 <p><a href='{safeEventUrl}'>View Event Details</a></p>
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

        private void LogConfigSource()
        {
            if (_configuration is not IConfigurationRoot root) return;

            foreach (var key in new[] { "EmailSettings:SenderEmail", "EmailSettings:Password" })
            {
                var provider = root.Providers.Reverse().FirstOrDefault(p => p.TryGet(key, out _));
                _logger.LogInformation("[EMAIL] {Key} is read from: {Provider}", key, provider?.ToString() ?? "(not found)");
            }
        }


        public async Task SendOrganizerRequestResultAsync(string toEmail, string fullName, bool approved, string? rejectionReason, string? reviewNote)
        {
            var subject = approved
                ? "[ConcertShield] Your Organizer Request Has Been Approved! 🎉"
                : "[ConcertShield] Update Regarding Your Organizer Request";

            var safeName = System.Net.WebUtility.HtmlEncode(fullName ?? "Valued Customer");
            var safeReason = System.Net.WebUtility.HtmlEncode(rejectionReason ?? "Information or documentation did not meet verification criteria.");
            var safeNote = System.Net.WebUtility.HtmlEncode(reviewNote ?? string.Empty);

            var reviewNoteHtml = string.IsNullOrWhiteSpace(safeNote)
                ? string.Empty
                : $@"<div style='background: #f3f4f6; border-left: 4px solid #6366f1; border-radius: 6px; padding: 12px 16px; margin: 16px 0; font-size: 14px; color: #374151;'>
                        <strong>Admin Note:</strong> {safeNote}
                     </div>";

            var bodyHtml = approved
                ? $@"<div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e5e7eb; border-radius: 12px; overflow: hidden;'>
                        <div style='background: linear-gradient(135deg, #4f46e5 0%, #7c3aed 100%); padding: 32px 24px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 700; letter-spacing: -0.5px;'>ConcertShield</h1>
                            <p style='margin: 8px 0 0; font-size: 15px; opacity: 0.9;'>Event Management & Anti-Scalping Platform</p>
                        </div>
                        <div style='padding: 32px 24px; color: #1f2937;'>
                            <div style='text-align: center; margin-bottom: 24px;'>
                                <span style='display: inline-block; width: 56px; height: 56px; line-height: 56px; border-radius: 50%; background: #dcfce7; color: #16a34a; font-size: 28px;'>✓</span>
                            </div>
                            <h2 style='margin: 0 0 16px; font-size: 20px; font-weight: 700; color: #111827; text-align: center;'>Organizer Request Approved!</h2>
                            <p style='margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #374151;'>
                                Dear <strong>{safeName}</strong>,
                            </p>
                            <p style='margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #374151;'>
                                Congratulations! Your application to become an Event Organizer on <strong>ConcertShield</strong> has been successfully reviewed and approved by our Administration team.
                            </p>
                            {reviewNoteHtml}
                            <div style='background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; padding: 16px; margin: 24px 0;'>
                                <h3 style='margin: 0 0 8px; font-size: 14px; text-transform: uppercase; letter-spacing: 0.5px; color: #111827;'>What's next?</h3>
                                <ul style='margin: 0; padding-left: 20px; color: #1f2937; font-size: 14px; line-height: 1.6;'>
                                    <li>Log in to your ConcertShield account and open the <strong>Organizer Dashboard</strong> to create and manage events.</li>
                                    <li>Configure ticket types, pricing, and anti-scalping shield policies.</li>
                                    <li>Monitor real-time sales and attendee check-ins.</li>
                                </ul>
                            </div>
                            <p style='margin: 24px 0 0; font-size: 13px; color: #4b5563; text-align: center;'>
                                This is an automated notification email. If you did not make this request or need assistance, please contact our support team.
                            </p>
                        </div>
                        <div style='background: #f9fafb; padding: 16px 24px; text-align: center; border-top: 1px solid #e5e7eb; font-size: 12px; color: #6b7280;'>
                            © {DateTime.UtcNow.Year} ConcertShield. All rights reserved.
                        </div>
                    </div>"
                : $@"<div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e5e7eb; border-radius: 12px; overflow: hidden;'>
                        <div style='background: linear-gradient(135deg, #1f2937 0%, #111827 100%); padding: 32px 24px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 700; letter-spacing: -0.5px;'>ConcertShield</h1>
                            <p style='margin: 8px 0 0; font-size: 15px; opacity: 0.9;'>Event Management & Anti-Scalping Platform</p>
                        </div>
                        <div style='padding: 32px 24px; color: #111827;'>
                            <div style='text-align: center; margin-bottom: 24px;'>
                                <span style='display: inline-block; width: 56px; height: 56px; line-height: 56px; border-radius: 50%; background: #fee2e2; color: #ef4444; font-size: 28px;'>✕</span>
                            </div>
                            <h2 style='margin: 0 0 16px; font-size: 20px; font-weight: 700; color: #111827; text-align: center;'>Organizer Request Declined</h2>
                            <p style='margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #111827;'>
                                Dear <strong>{safeName}</strong>,
                            </p>
                            <p style='margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #1f2937;'>
                                Thank you for your interest in becoming an Event Organizer on ConcertShield. After reviewing the business information and legal documents you provided, we are unable to approve your application at this time.
                            </p>
                            <div style='background: #fef2f2; border: 1px solid #fecaca; border-left: 4px solid #ef4444; border-radius: 6px; padding: 14px 16px; margin: 20px 0;'>
                                <div style='font-size: 13px; font-weight: 700; text-transform: uppercase; color: #991b1b; margin-bottom: 4px;'>Reason for Rejection:</div>
                                <div style='font-size: 14px; color: #7f1d1d; line-height: 1.5;'>{safeReason}</div>
                            </div>
                            {reviewNoteHtml}
                            <p style='margin: 20px 0 16px; font-size: 14px; line-height: 1.6; color: #1f2937;'>
                                Please review the feedback above for details regarding your application decision.
                            </p>
                            <p style='margin: 24px 0 0; font-size: 13px; color: #4b5563; text-align: center;'>
                                This is an automated notification email. If you believe this decision was made in error, please contact our support team.
                            </p>
                        </div>
                        <div style='background: #f9fafb; padding: 16px 24px; text-align: center; border-top: 1px solid #e5e7eb; font-size: 12px; color: #6b7280;'>
                            © {DateTime.UtcNow.Year} ConcertShield. All rights reserved.
                        </div>
                    </div>";

            await SendAsync(toEmail, subject, bodyHtml);
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