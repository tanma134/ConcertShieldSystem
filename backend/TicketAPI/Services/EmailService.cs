namespace TicketAPI.Services
{

    using System.Globalization;
    using System.Net;
    using System.Text;
    using global::TicketAPI.Models;
    using MailKit.Net.Smtp;
    using MailKit.Security;
    using MimeKit;

    namespace TicketAPI.Services
    {
        public class EmailService : IEmailService
        {
            private readonly IConfiguration _config;

            public EmailService(IConfiguration config)
            {
                _config = config;
            }

            public async Task SendPaymentSuccessAsync(
                Order order,
                List<Ticket> tickets)
            {
                if (string.IsNullOrWhiteSpace(order.Email))
                {
                    throw new InvalidOperationException(
                        $"Order {order.OrderId} does not have a customer email.");
                }

                var fromEmail = _config["EmailSettings:FromEmail"]
                    ?? throw new InvalidOperationException(
                        "EmailSettings:FromEmail is missing.");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("ConcertShield", fromEmail));
                message.To.Add(
                    new MailboxAddress(order.FullName ?? "", order.Email));
                message.Subject =
                    $"Payment confirmation - Order #{order.OrderId}";

                var ticketCards = new StringBuilder();

                foreach (var ticket in tickets)
                {
                    ticketCards.Append($@"
                    <div class=""ticket"">
                        <h3>{Encode(ticket.TicketTypeName ?? "Event ticket")}</h3>
                        <p>
                            <strong>Attendee:</strong>
                            {Encode(ticket.OwnerName)}
                        </p>
                        <p class=""ticket-note"">
                            View your ticket and check-in QR code in your account on our website.
                        </p>
                    </div>");
                }

                var eventName = Encode(order.EventName);
                var customerName = Encode(order.FullName);
                var email = Encode(order.Email);
                var phone = Encode(order.Phone);
                var eventStart = FormatDate(order.StartsAt);
                var eventEnd = FormatDate(order.EndsAt);
                var orderDate = FormatDate(order.OrderDate);
                var total = order.FinalAmount.ToString(
                    "N0",
                    CultureInfo.GetCultureInfo("en-US"));

                var posterHtml = string.IsNullOrWhiteSpace(order.PosterUrl)
                ? ""
                : $@"
                    <img
                        src=""{WebUtility.HtmlEncode(order.PosterUrl)}""
                        alt=""{WebUtility.HtmlEncode(order.EventName)} poster""
                        style=""display:block;width:100%;max-width:520px;height:auto;margin:0 auto 20px;border-radius:10px;""
                    />";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                    <!DOCTYPE html>
                    <html lang=""en"">
                    <head>
                        <meta charset=""UTF-8"">
                    </head>
                    <body style=""margin:0;background:#f4f4f7;font-family:Arial,sans-serif;color:#24212b;"">
                        <div style=""max-width:640px;margin:24px auto;background:#fff;border-radius:12px;overflow:hidden;"">
                            <div style=""background:#7027c8;color:#fff;padding:28px;text-align:center;"">
                                <h1 style=""margin:0 0 8px;"">Payment successful</h1>
                                <p style=""margin:0;"">Thank you for booking with ConcertShield.</p>
                            </div>

                            <div style=""padding:28px;"">
                                <h2>Order information</h2>
                                <p><strong>Order ID:</strong> #{order.OrderId}</p>
                                <p><strong>Customer:</strong> {customerName}</p>
                                <p><strong>Email:</strong> {email}</p>
                                <p><strong>Phone:</strong> {phone}</p>
                                <p><strong>Order date:</strong> {orderDate}</p>
                                <p><strong>Total paid:</strong> {total} VND</p>

                                <hr style=""border:0;border-top:1px solid #e7e4eb;margin:24px 0;"">

                                <h2>Event information</h2>
                                {posterHtml}
                                <p><strong>Event:</strong> {eventName}</p>
                                <p><strong>Starts:</strong> {eventStart}</p>
                                <p><strong>Ends:</strong> {eventEnd}</p>

                                <hr style=""border:0;border-top:1px solid #e7e4eb;margin:24px 0;"">

                                <h2>Your tickets ({tickets.Count})</h2>
                                {ticketCards}

                                <p style=""margin-top:24px;color:#625c6b;"">
                                    Sign in to our website and open My Tickets to view your tickets,
                                    seat details, and check-in QR codes.
                                </p>
                            </div>

                            <div style=""padding:18px;text-align:center;background:#f8f7fa;color:#777;font-size:12px;"">
                                This is an automated email. Please do not reply.
                            </div>
                        </div>
                    </body>
                    </html>"
                };

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();

                var smtpServer = _config["EmailSettings:SmtpServer"]
                    ?? throw new InvalidOperationException(
                        "EmailSettings:SmtpServer is missing.");

                if (!int.TryParse(
                        _config["EmailSettings:SmtpPort"],
                        out var smtpPort))
                {
                    throw new InvalidOperationException(
                        "EmailSettings:SmtpPort is invalid.");
                }

                var smtpUsername = _config["EmailSettings:SmtpUsername"]
                    ?? throw new InvalidOperationException(
                        "EmailSettings:SmtpUsername is missing.");

                var smtpPassword = _config["EmailSettings:SmtpPassword"]
                    ?? throw new InvalidOperationException(
                        "EmailSettings:SmtpPassword is missing.");

                await client.ConnectAsync(
                    smtpServer,
                    smtpPort,
                    SecureSocketOptions.StartTls);

                await client.AuthenticateAsync(smtpUsername, smtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            private static string Encode(string? value)
            {
                return WebUtility.HtmlEncode(
                    string.IsNullOrWhiteSpace(value) ? "—" : value);
            }

            private static string FormatDate(DateTime? value)
            {
                return value?.ToLocalTime().ToString(
                    "dd MMM yyyy, hh:mm tt",
                    CultureInfo.GetCultureInfo("en-US")) ?? "Not available";
            }
        }
    }
}
