using System.Text.RegularExpressions;
namespace TicketAPI.Services;

// Validation of the "View Affected Orders/Tickets" filters; pure so it can be tested without a database.
public static class AffectedFilterRules
{
    public static readonly string[] AllowedStatuses = { "Pending", "Notified", "RefundPending", "Refunded", "RefundFailed", "Returned", "Active" };

    // Returns the numeric order id when an order code was given, otherwise null.
    public static int? Validate(string tab, string? status, string? orderCode, int? userId, int page, int pageSize)
    {
        if (tab != "orders" && tab != "tickets" || page < 1 || pageSize < 1 || pageSize > 100 || userId <= 0)
            throw new ArgumentException("Invalid filters or pagination.");
        if (!string.IsNullOrEmpty(status) && !AllowedStatuses.Contains(status))
            throw new ArgumentException("Invalid processing status.");
        if (string.IsNullOrWhiteSpace(orderCode)) return null;
        if (!Regex.IsMatch(orderCode, @"^(ORD-)?[1-9][0-9]*$") || !int.TryParse(orderCode.Replace("ORD-", ""), out var parsed))
            throw new ArgumentException("Invalid order code.");
        return parsed;
    }
}
