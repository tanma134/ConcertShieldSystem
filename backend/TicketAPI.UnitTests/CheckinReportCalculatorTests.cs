using TicketAPI.Domain;
using Xunit;

namespace TicketAPI.UnitTests;

// UC_14.2: the numbers at the top of the exported check-in report.
public class CheckinReportCalculatorTests
{
    private static readonly DateTime At = new(2026, 10, 3, 19, 0, 0, DateTimeKind.Utc);

    private static CheckinTicketFact Ticket(int id, string type, string status, bool checkedIn) =>
        new(id, type, status, checkedIn ? At : null);

    [Fact]
    public void RateIsCheckedInOverValidTickets()
    {
        var summary = CheckinReportCalculator.Summarize(new[]
        {
            Ticket(1, "VIP", TicketStatuses.Active, true),
            Ticket(2, "VIP", TicketStatuses.Active, false),
            Ticket(3, "Standard", TicketStatuses.Active, true),
            Ticket(4, "Standard", TicketStatuses.Active, true),
        });

        Assert.Equal(4, summary.TotalTickets);
        Assert.Equal(3, summary.CheckedIn);
        Assert.Equal(1, summary.NotCheckedIn);
        Assert.Equal(75.0, summary.CheckInRatePercent);
    }

    [Fact]
    public void ReturnedTicketsAreLeftOutOfTheRate()
    {
        var summary = CheckinReportCalculator.Summarize(new[]
        {
            Ticket(1, "VIP", TicketStatuses.Active, true),
            Ticket(2, "VIP", TicketStatuses.Returned, false),
        });

        Assert.Equal(1, summary.Returned);
        Assert.Equal(100.0, summary.CheckInRatePercent);
    }

    [Fact]
    public void RateIsZeroWhenThereAreNoValidTickets()
    {
        var summary = CheckinReportCalculator.Summarize(Array.Empty<CheckinTicketFact>());

        Assert.Equal(0.0, summary.CheckInRatePercent);
        Assert.Equal(0, summary.TotalTickets);
    }

    [Fact]
    public void BreakdownIsGroupedByTicketType()
    {
        var summary = CheckinReportCalculator.Summarize(new[]
        {
            Ticket(1, "VIP", TicketStatuses.Active, true),
            Ticket(2, "VIP", TicketStatuses.Active, false),
            Ticket(3, "Standard", TicketStatuses.Active, true),
        });

        var vip = summary.ByTicketType.Single(t => t.TicketTypeName == "VIP");
        Assert.Equal(2, vip.Total);
        Assert.Equal(1, vip.CheckedIn);
        Assert.Equal(50.0, vip.RatePercent);
    }
}
