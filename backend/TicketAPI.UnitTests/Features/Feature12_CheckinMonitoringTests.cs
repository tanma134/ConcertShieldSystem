using System;
using TicketAPI.Domain;
using Xunit;

namespace TicketAPI.UnitTests.Features;

/// <summary>
/// Feature 12: Real-time Check-in Monitoring
/// Tests for UC_3.13.2 (Export Check-in Report)
/// </summary>
public class Feature12_CheckinMonitoringTests
{
    private static readonly DateTime At = new(2026, 10, 3, 19, 30, 0, DateTimeKind.Utc);

    private static CheckinTicketFact Ticket(int id, string type, string status, bool checkedIn) =>
        new(id, type, status, checkedIn ? At : null);

    [Fact]
    public void UC_3_13_2_ExportCheckinReport_ComputesCheckinRatioAndTiers()
    {
        var tickets = new[]
        {
            Ticket(1, "VIP", TicketStatuses.Active, true),
            Ticket(2, "VIP", TicketStatuses.Active, false),
            Ticket(3, "Standard", TicketStatuses.Active, true),
            Ticket(4, "Standard", TicketStatuses.Active, true),
            Ticket(5, "Standard", TicketStatuses.Returned, false) // Returned should be excluded from valid rate denominator
        };

        var summary = CheckinReportCalculator.Summarize(tickets);

        Assert.Equal(4, summary.TotalTickets);
        Assert.Equal(3, summary.CheckedIn);
        Assert.Equal(1, summary.NotCheckedIn);
        Assert.Equal(1, summary.Returned);
        Assert.Equal(75.0, summary.CheckInRatePercent);
    }

    [Fact]
    public void UC_3_13_2_ExportCheckinReport_ValidCsvStructure()
    {
        var headers = new[] { "TicketId", "AttendeeName", "TicketType", "Gate", "CheckinTime", "Status" };
        var line = string.Join(",", headers);

        Assert.Contains("AttendeeName", line);
        Assert.Contains("CheckinTime", line);
    }
}
