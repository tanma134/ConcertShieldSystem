namespace EventAPI.Common;

public static class RefundPolicyRules
{
    public const string SalesLeadWarning =
        "The refund deadline is greater than or equal to the time between ticket sales opening and the event. Tickets may not be eligible for customer-requested refunds.";

    public static string? GetSalesLeadWarning(DateTime? salesStartsAt, DateTime eventStartsAt, int deadlineBeforeEventHours)
    {
        if (!salesStartsAt.HasValue || eventStartsAt == default || deadlineBeforeEventHours < 0)
            return null;

        var salesLeadHours = (eventStartsAt - salesStartsAt.Value).TotalHours;
        if (salesLeadHours < 0 || double.IsNaN(salesLeadHours) || double.IsInfinity(salesLeadHours))
            return null;

        return deadlineBeforeEventHours >= salesLeadHours ? SalesLeadWarning : null;
    }
}
