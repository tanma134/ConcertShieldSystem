namespace TicketAPI.Domain;

// Result of the eligibility check. Code is a stable machine-readable reason
// (the frontend shows Message, tests assert on Code).
// RefundPercent is the share of the ticket price given back when the ticket is eligible.
public sealed record ReturnEligibility(bool IsEligible, string Code, string Message, decimal RefundPercent = 100m);

// One refund tier configured by the organizer: "RefundPercent % back up to
// DeadlineHours hours before the event starts".
public sealed record RefundTier(int DeadlineHours, decimal RefundPercent);

// Everything the policy needs to know about a ticket, collected by the service.
public sealed record TicketReturnFacts(
    string? TicketStatus,
    string? OrderStatus,
    DateTime? CheckedInAt,
    DateTime EventStartsAtUtc,
    bool HasOpenRequest);

// Result of validating the free-text reason typed by the customer.
public sealed record ReasonValidation(bool IsValid, string Reason, string? Error);

// Business rules for UC_9 (ticket return). Pure functions: no database, no clock.
public static class TicketReturnPolicy
{
    // Returns close this many hours before the event starts.
    public const int DefaultCutoffHours = 24;

    public const int MinReasonLength = 10;
    public const int MaxReasonLength = 500;

    // Tier used when the organizer has not configured any active refund policy.
    public static readonly RefundTier DefaultTier = new(DefaultCutoffHours, 100m);

    // Decides whether a ticket may enter the return flow right now, using the
    // default tier. Kept for callers that have no organizer policy.
    public static ReturnEligibility Evaluate(
        TicketReturnFacts facts,
        DateTime nowUtc,
        int cutoffHours = DefaultCutoffHours)
    {
        return Evaluate(facts, nowUtc, new[] { new RefundTier(cutoffHours, 100m) });
    }

    // Decides whether a ticket may enter the return flow right now, using the
    // refund tiers the organizer configured for the event.
    // Rules are checked from the most specific to the most general so the
    // customer always sees the most useful message.
    // A null/empty tier list means the organizer set nothing, so the default applies.
    public static ReturnEligibility Evaluate(
        TicketReturnFacts facts,
        DateTime nowUtc,
        IReadOnlyList<RefundTier>? tiers)
    {
        if (!string.Equals(facts.OrderStatus, "Paid", StringComparison.Ordinal))
            return Reject("ORDER_NOT_PAID", "Only tickets from a paid order can be returned.");

        if (facts.CheckedInAt.HasValue)
            return Reject("ALREADY_CHECKED_IN", "This ticket has already been used to enter the event.");

        if (facts.HasOpenRequest ||
            string.Equals(facts.TicketStatus, TicketStatuses.ReturnPending, StringComparison.Ordinal))
            return Reject("RETURN_ALREADY_REQUESTED", "A return request for this ticket is already waiting for review.");

        if (string.Equals(facts.TicketStatus, TicketStatuses.Returned, StringComparison.Ordinal))
            return Reject("ALREADY_RETURNED", "This ticket has already been returned.");

        if (!string.Equals(facts.TicketStatus, TicketStatuses.Active, StringComparison.Ordinal))
            return Reject("TICKET_NOT_ACTIVE", "This ticket is not active, so it cannot be returned.");

        var effective = tiers is { Count: > 0 } ? tiers : new[] { DefaultTier };
        var tier = SelectTier(effective, facts.EventStartsAtUtc - nowUtc);
        if (tier == null)
        {
            var widest = effective.Max(t => t.DeadlineHours);
            return Reject("RETURN_WINDOW_CLOSED",
                $"Returns close {widest} hours before the event starts.");
        }

        return new ReturnEligibility(true, "OK", "This ticket can be returned.", tier.RefundPercent);
    }

    // Picks the tier that applies "right now". A tier applies while at least
    // DeadlineHours remain before the event; when several apply, the one with the
    // longest deadline wins because it is the most generous tier the buyer still
    // qualifies for. Returns null when every deadline has already passed.
    public static RefundTier? SelectTier(IEnumerable<RefundTier> tiers, TimeSpan untilEvent)
    {
        return tiers
            .Where(t => untilEvent >= TimeSpan.FromHours(t.DeadlineHours))
            .OrderByDescending(t => t.DeadlineHours)
            .ThenByDescending(t => t.RefundPercent)
            .FirstOrDefault();
    }

    // Applies the tier percentage to an amount, rounding down to whole VND.
    // The percentage is clamped to 0..100 so a bad policy can never refund more than was paid.
    public static long ApplyPercent(long amount, decimal percent)
    {
        var clamped = Math.Min(Math.Max(percent, 0m), 100m);
        return (long)Math.Floor(Math.Max(amount, 0) * clamped / 100m);
    }

    // Trims the reason and checks its length. The trimmed text is what gets stored.
    public static ReasonValidation ValidateReason(string? reason)
    {
        var trimmed = (reason ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            return new ReasonValidation(false, trimmed, "Please tell us why you want to return this ticket.");

        if (trimmed.Length < MinReasonLength)
            return new ReasonValidation(false, trimmed,
                $"The reason must be at least {MinReasonLength} characters.");

        if (trimmed.Length > MaxReasonLength)
            return new ReasonValidation(false, trimmed,
                $"The reason must be at most {MaxReasonLength} characters.");

        return new ReasonValidation(true, trimmed, null);
    }

    // Money given back for one ticket: its price minus its share of the order discount.
    // A zero-value order has no discount to share.
    public static long RefundAmount(long unitPrice, long orderGross, long? orderDiscount)
    {
        if (orderGross <= 0 || orderDiscount is null or <= 0)
            return Math.Max(unitPrice, 0);

        var share = (long)((decimal)orderDiscount.Value * unitPrice / orderGross);
        return Math.Max(unitPrice - share, 0);
    }

    // A customer may only withdraw a request that has not been decided yet.
    public static bool CanCancel(string? requestStatus)
    {
        return ReturnRequestStatuses.IsOpen(requestStatus);
    }

    // Small helper so every rejection is built the same way.
    private static ReturnEligibility Reject(string code, string message)
    {
        return new ReturnEligibility(false, code, message);
    }
}

// Chính sách nền tảng cho khách bị ảnh hưởng bởi đổi lịch; không dùng ngày giả khi chưa có lịch mới.
public static class EventChangeReturnPolicy
{
    public static ReturnEligibility Evaluate(string? ticketStatus, string? orderStatus, DateTime? checkedInAt, bool hasOpenRequest, DateTime now, DateTime appliedAt, int windowHours, decimal percent)
    {
        if (orderStatus != "Paid" || ticketStatus != TicketStatuses.Active || checkedInAt.HasValue || hasOpenRequest)
            return new(false, "TICKET_NOT_ELIGIBLE", "The ticket is not eligible for a return request.", 0);
        if (windowHours < 1 || now > appliedAt.AddHours(windowHours))
            return new(false, "CHANGE_RETURN_WINDOW_CLOSED", "The return window for this schedule change has closed.", 0);
        return new(true, "EVENT_CHANGED", "You can request a return under the schedule-change policy.", Math.Clamp(percent, 0, 100));
    }
}
