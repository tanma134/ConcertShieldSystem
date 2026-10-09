namespace TicketAPI.Domain;

// Status values stored in tickets.status.
// Active        -> valid for entry (set when the order is paid).
// ReturnPending -> the owner asked to return it; blocked from QR rotation and check-in.
// Returned      -> the return was approved; the ticket can no longer be used.
public static class TicketStatuses
{
    public const string Active = "Active";
    public const string ReturnPending = "ReturnPending";
    public const string Returned = "Returned";
}

// Status values stored in ticket_return_requests.status.
// Pending is the only open state; Approved and RefundFailed can still move to Refunded.
public static class ReturnRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    // Approved   -> staff accepted the return; the money has not been paid back yet.
    // Refunded   -> the money was paid back to the customer.
    // RefundFailed -> approved, but paying the money back failed; staff can retry.
    public const string Refunded = "Refunded";
    public const string RefundFailed = "RefundFailed";

    // Approved, Refunded and RefundFailed all count as a refund in revenue reports.
    public static bool IsApprovedFamily(string? status)
    {
        return status == Approved || status == Refunded || status == RefundFailed;
    }

    // True while the request is still waiting for a decision.
    public static bool IsOpen(string? status)
    {
        return string.Equals(status, Pending, StringComparison.Ordinal);
    }
}
