namespace TicketAPI.Domain;

// One ticket as seen by the check-in report.
public sealed record CheckinTicketFact(int TicketId, string TicketTypeName, string Status, DateTime? CheckedInAt);

// Check-in numbers for one ticket type.
public sealed record CheckinByTicketType(string TicketTypeName, int Total, int CheckedIn, double RatePercent);

// Check-in numbers for the whole event.
// TotalTickets counts tickets still valid for entry (returned tickets are left out).
public sealed record CheckinSummary(
    int TotalTickets,
    int CheckedIn,
    int NotCheckedIn,
    int Returned,
    double CheckInRatePercent,
    IReadOnlyList<CheckinByTicketType> ByTicketType);

// Computes the figures shown at the top of the check-in report (UC_14.2).
public static class CheckinReportCalculator
{
    // Counts valid / checked-in / returned tickets and the check-in rate.
    public static CheckinSummary Summarize(IEnumerable<CheckinTicketFact> tickets)
    {
        var all = tickets.ToList();
        var valid = all.Where(t => t.Status != TicketStatuses.Returned).ToList();
        var checkedIn = valid.Count(t => t.CheckedInAt.HasValue);

        var byType = valid
            .GroupBy(t => t.TicketTypeName)
            .Select(group =>
            {
                var done = group.Count(t => t.CheckedInAt.HasValue);
                return new CheckinByTicketType(group.Key, group.Count(), done, Rate(done, group.Count()));
            })
            .OrderBy(item => item.TicketTypeName)
            .ToList();

        return new CheckinSummary(
            valid.Count,
            checkedIn,
            valid.Count - checkedIn,
            all.Count - valid.Count,
            Rate(checkedIn, valid.Count),
            byType);
    }

    // Percentage with one decimal; zero when there is nothing to divide by.
    private static double Rate(int part, int total)
    {
        return total == 0 ? 0.0 : Math.Round(part * 100.0 / total, 1);
    }
}
