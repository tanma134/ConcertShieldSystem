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

        public async Task SendOrganizerRequestResultAsync(string toEmail, string fullName, bool approved, string? rejectionReason, string? reviewNote)
        {
            var settings = _configuration.GetSection("EmailSettings");
            var host = settings["Host"] ?? "smtp.gmail.com";
            var port = int.TryParse(settings["Port"], out var p) ? p : 587;
            var enableSsl = bool.TryParse(settings["EnableSsl"], out var ssl) ? ssl : true;
            var senderEmail = settings["SenderEmail"] ?? "tanmnce182352@fpt.edu.vn";
            var senderName = settings["SenderName"] ?? "ConcertShield";
            var password = settings["Password"];
            if (string.IsNullOrWhiteSpace(password)) password = "juxdyjjrlztfmtsu";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = approved
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
                                <h3 style='margin: 0 0 8px; font-size: 14px; text-transform: uppercase; letter-spacing: 0.5px; color: #6b7280;'>What's next?</h3>
                                <ul style='margin: 0; padding-left: 20px; color: #4b5563; font-size: 14px; line-height: 1.6;'>
                                    <li>Access your <strong>Organizer Dashboard</strong> to create and manage events.</li>
                                    <li>Configure ticket types, pricing, and anti-scalping shield policies.</li>
                                    <li>Monitor real-time sales and attendee check-ins.</li>
                                </ul>
                            </div>
                            <div style='text-align: center; margin: 32px 0 16px;'>
                                <a href='https://localhost:5173/organizer/dashboard' style='display: inline-block; background: #4f46e5; color: #ffffff; padding: 14px 28px; font-size: 15px; font-weight: 600; text-decoration: none; border-radius: 8px;'>Go to Organizer Dashboard</a>
                            </div>
                            <p style='margin: 24px 0 0; font-size: 13px; color: #9ca3af; text-align: center;'>
                                If you did not make this request or need assistance, please contact our support team.
                            </p>
                        </div>
                        <div style='background: #f9fafb; padding: 16px 24px; text-align: center; border-top: 1px solid #e5e7eb; font-size: 12px; color: #9ca3af;'>
                            © {DateTime.UtcNow.Year} ConcertShield. All rights reserved.
                        </div>
                    </div>"
                : $@"<div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e5e7eb; border-radius: 12px; overflow: hidden;'>
                        <div style='background: linear-gradient(135deg, #1f2937 0%, #111827 100%); padding: 32px 24px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 700; letter-spacing: -0.5px;'>ConcertShield</h1>
                            <p style='margin: 8px 0 0; font-size: 15px; opacity: 0.9;'>Event Management & Anti-Scalping Platform</p>
                        </div>
                        <div style='padding: 32px 24px; color: #1f2937;'>
                            <div style='text-align: center; margin-bottom: 24px;'>
                                <span style='display: inline-block; width: 56px; height: 56px; line-height: 56px; border-radius: 50%; background: #fee2e2; color: #ef4444; font-size: 28px;'>✕</span>
                            </div>
                            <h2 style='margin: 0 0 16px; font-size: 20px; font-weight: 700; color: #111827; text-align: center;'>Organizer Request Declined</h2>
                            <p style='margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #374151;'>
                                Dear <strong>{safeName}</strong>,
                            </p>
                            <p style='margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #374151;'>
                                Thank you for your interest in becoming an Event Organizer on ConcertShield. After reviewing the business information and legal documents you provided, we are unable to approve your application at this time.
                            </p>
                            <div style='background: #fef2f2; border: 1px solid #fecaca; border-left: 4px solid #ef4444; border-radius: 6px; padding: 14px 16px; margin: 20px 0;'>
                                <div style='font-size: 13px; font-weight: 700; text-transform: uppercase; color: #991b1b; margin-bottom: 4px;'>Reason for Rejection:</div>
                                <div style='font-size: 14px; color: #7f1d1d; line-height: 1.5;'>{safeReason}</div>
                            </div>
                            {reviewNoteHtml}
                            <p style='margin: 20px 0 16px; font-size: 14px; line-height: 1.6; color: #4b5563;'>
                                You are welcome to resolve the points mentioned above, update your documentation, and submit a new organizer registration request at any time.
                            </p>
                            <div style='text-align: center; margin: 28px 0 16px;'>
                                <a href='https://localhost:5173/organizer/request' style='display: inline-block; background: #374151; color: #ffffff; padding: 12px 24px; font-size: 14px; font-weight: 600; text-decoration: none; border-radius: 8px;'>Submit New Request</a>
                            </div>
                            <p style='margin: 24px 0 0; font-size: 13px; color: #9ca3af; text-align: center;'>
                                If you believe this decision was made in error, please contact our support team.
                            </p>
                        </div>
                        <div style='background: #f9fafb; padding: 16px 24px; text-align: center; border-top: 1px solid #e5e7eb; font-size: 12px; color: #9ca3af;'>
                            © {DateTime.UtcNow.Year} ConcertShield. All rights reserved.
                        </div>
                    </div>";

            message.Body = new TextPart("html") { Text = bodyHtml };

            using var client = new SmtpClient();
            var secureOption = enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(host, port, secureOption);
            await client.AuthenticateAsync(senderEmail, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
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