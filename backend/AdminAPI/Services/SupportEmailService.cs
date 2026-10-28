using System.Net;
using System.Net.Mail;

namespace AdminAPI.Services;

// Implementation of email notifications for support case resolution outcomes
public class SupportEmailService : ISupportEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SupportEmailService> _logger;

    public SupportEmailService(IConfiguration config, ILogger<SupportEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendCaseResolvedEmailAsync(
        string toEmail,
        string customerName,
        string caseNumber,
        string caseTitle,
        string resolution,
        string resolvedByName,
        int? orderId)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Cannot send case resolution email: recipient email is empty for case #{CaseNumber}", caseNumber);
            return;
        }

        var subject = $"[ConcertShield Support] Case #{caseNumber} has been resolved";
        var orderContext = orderId.HasValue ? $"regarding your Order #{orderId.Value}" : "";

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #0b0f19; color: #f9fafb; margin: 0; padding: 24px; }}
        .card {{ background-color: #1f2937; border: 1px solid #374151; border-radius: 12px; padding: 28px; max-width: 600px; margin: 0 auto; }}
        .badge {{ background-color: rgba(16, 185, 129, 0.15); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.3); padding: 4px 12px; border-radius: 9999px; font-size: 12px; font-weight: bold; text-transform: uppercase; }}
        h2 {{ color: #ffffff; margin-top: 14px; font-size: 20px; }}
        .resolution-box {{ background-color: #111827; border-left: 4px solid #10b981; padding: 16px; border-radius: 8px; margin: 20px 0; color: #e5e7eb; line-height: 1.6; }}
        .footer {{ font-size: 12px; color: #9ca3af; margin-top: 24px; border-top: 1px solid #374151; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <span class='badge'>Case Resolved</span>
        <h2>Support Case #{caseNumber} Update</h2>
        <p>Dear <strong>{customerName}</strong>,</p>
        <p>Your support request <em>""{caseTitle}""</em> {orderContext} has been reviewed and resolved by our customer support team.</p>
        
        <div class='resolution-box'>
            <strong>Resolution Outcome:</strong><br />
            {WebUtility.HtmlEncode(resolution)}
        </div>

        <p>Handled by: <strong>{resolvedByName}</strong> on {DateTime.UtcNow:MMM dd, yyyy HH:mm} UTC</p>
        <p>If you have any further questions or require additional assistance, please reply to this email or visit your order details on ConcertShield.</p>

        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} ConcertShield Platform. All rights reserved.
        </div>
    </div>
</body>
</html>";

        await SendMailDeliveryAsync(toEmail, subject, body, caseNumber, $"Resolution notification for customer {customerName}");
    }

    public async Task SendCaseAssignedEmailAsync(
        string toEmail,
        string staffName,
        string caseNumber,
        string caseTitle,
        string priority,
        string category,
        string customerName,
        int? orderId,
        int caseId)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Cannot send case assignment email: recipient email is empty for case #{CaseNumber}", caseNumber);
            return;
        }

        var subject = $"[ConcertShield Staff] New Support Case Assigned - #{caseNumber} ({priority})";
        var orderContext = orderId.HasValue ? $"Order #{orderId.Value}" : "None";
        var caseUrl = $"http://localhost:5173/staff/support-cases/{caseId}";
        var isHighPriority = priority.Equals("High", StringComparison.OrdinalIgnoreCase) || priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase);

        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #0b0f19; color: #f9fafb; margin: 0; padding: 24px; }}
        .card {{ background-color: #1f2937; border: 1px solid #374151; border-radius: 12px; padding: 28px; max-width: 600px; margin: 0 auto; }}
        .badge {{ background-color: rgba(59, 130, 246, 0.15); color: #60a5fa; border: 1px solid rgba(59, 130, 246, 0.3); padding: 4px 12px; border-radius: 9999px; font-size: 12px; font-weight: bold; text-transform: uppercase; }}
        .badge.high {{ background-color: rgba(239, 68, 68, 0.15); color: #f87171; border-color: rgba(239, 68, 68, 0.3); }}
        h2 {{ color: #ffffff; margin-top: 14px; font-size: 20px; }}
        .details-box {{ background-color: #111827; border: 1px solid #374151; padding: 16px; border-radius: 8px; margin: 20px 0; color: #e5e7eb; line-height: 1.8; font-size: 14px; }}
        .btn {{ display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; padding: 12px 24px; border-radius: 8px; font-weight: bold; margin-top: 16px; }}
        .footer {{ font-size: 12px; color: #9ca3af; margin-top: 24px; border-top: 1px solid #374151; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <span class='badge {(isHighPriority ? "high" : "")}'>Case Assigned • {priority} Priority</span>
        <h2>New Support Assignment: #{caseNumber}</h2>
        <p>Hello <strong>{staffName}</strong>,</p>
        <p>You have been assigned by an Administrator to investigate and resolve the following support inquiry:</p>
        
        <div class='details-box'>
            <div><strong>Subject:</strong> {WebUtility.HtmlEncode(caseTitle)}</div>
            <div><strong>Category:</strong> {category}</div>
            <div><strong>Customer:</strong> {customerName}</div>
            <div><strong>Linked Order:</strong> {orderContext}</div>
            <div><strong>Assigned At:</strong> {DateTime.UtcNow:MMM dd, yyyy HH:mm} UTC</div>
        </div>

        <p>Please review customer details and transaction records to initiate resolution:</p>
        <a href='{caseUrl}' class='btn'>View &amp; Process Support Case</a>

        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} ConcertShield Platform Internal Support System.
        </div>
    </div>
</body>
</html>";

        await SendMailDeliveryAsync(toEmail, subject, body, caseNumber, $"Staff assignment notification for {staffName}");
    }

    private async Task SendMailDeliveryAsync(string toEmail, string subject, string body, string caseNumber, string logSummary)
    {
        var host = _config["EmailSettings:Host"];
        var portStr = _config["EmailSettings:Port"];
        var senderEmail = _config["EmailSettings:SenderEmail"] ?? "support@concertshield.com";
        var senderName = _config["EmailSettings:SenderName"] ?? "ConcertShield Support";
        var password = _config["EmailSettings:Password"];

        if (!string.IsNullOrWhiteSpace(host) && int.TryParse(portStr, out var port) && !string.IsNullOrWhiteSpace(password))
        {
            try
            {
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(senderEmail, password)
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mail.To.Add(toEmail);

                await client.SendMailAsync(mail);
                _logger.LogInformation("Email sent successfully to {ToEmail} for case #{CaseNumber} ({Summary})", toEmail, caseNumber, logSummary);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email via SMTP to {ToEmail}. Fallback to notification log.", toEmail);
            }
        }

        // Graceful log fallback (for local development & demo without SMTP password)
        _logger.LogInformation("[DEMO NOTIFICATION] Support Case #{CaseNumber} email prepared for {ToEmail} ({Summary})",
            caseNumber, toEmail, logSummary);
    }
}
