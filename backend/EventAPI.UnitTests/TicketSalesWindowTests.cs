using EventAPI.Common;
using EventAPI.Models;
using EventAPI.Services;
using Xunit;

namespace EventAPI.UnitTests;

public class TicketSalesWindowTests
{
    [Theory]
    [InlineData(6, false, true)]
    [InlineData(4, false, false)]
    [InlineData(5, false, false)]
    [InlineData(4, true, true)]
    public void EventSalesRespectDefaultCutoffAndExplicitOverride(int hoursUntilConcert, bool explicitEnd, bool expected)
    {
        var now = DateTime.UtcNow;
        var ev = new Event
        {
            StartsAt = now.AddHours(hoursUntilConcert), Status = EventStatus.Published,
            ComplianceStatus = "Approved", ComplianceVersion = 1, ComplianceReviewedVersion = 1,
            TicketTypes = new List<TicketType>
            {
                new() { Status = "Active", Quantity = 100,
                    SalesEndsAt = explicitEnd ? now.AddHours(1) : null }
            }
        };

        Assert.Equal(expected, EventService.MapToResponse(ev).CanSell);
    }

    [Fact]
    public void DefaultEndTracksConcertScheduleAndExplicitEndIsPreserved()
    {
        var start = new DateTime(2026, 12, 30, 19, 0, 0, DateTimeKind.Utc);
        Assert.Equal(start.AddHours(-5), TicketSalesWindow.EndsAt(null, start));
        Assert.Equal(start.AddDays(1).AddHours(-5), TicketSalesWindow.EndsAt(null, start.AddDays(1)));
        Assert.Equal(start.AddHours(-2), TicketSalesWindow.EndsAt(start.AddHours(-2), start));
    }
}
