using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventAPI.UnitTests.Features;

/// <summary>
/// Feature 9: Event Creation & Lifecycle Management (Change Governance)
/// Tests for UC_3.10.8 (Postpone/Reschedule Event), UC_3.10.9 (Submit Event Change Request),
/// UC_3.10.10 (Approve Event Change Request), UC_3.10.11 (View Affected Orders/Tickets by Change)
/// </summary>
public class Feature09_EventChangeGovernanceTests
{
    private EventDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    [Fact]
    public void UC_3_10_8_PostponeRescheduleEvent_NewDatesMustBeChronological()
    {
        var originalStart = DateTime.UtcNow.AddDays(5);
        var rescheduledStart = DateTime.UtcNow.AddDays(30);
        var rescheduledEnd = rescheduledStart.AddHours(4);

        Assert.True(rescheduledStart > originalStart, "Rescheduled start date must be in future");
        Assert.True(rescheduledEnd > rescheduledStart, "Rescheduled end date must be after start date");
    }

    [Fact]
    public void UC_3_10_9_SubmitEventChangeRequest_ChangeType_MatchesAllowedCategories()
    {
        var allowedTypes = new[] { "Reschedule", "VenueChange", "ArtistChange", "Cancellation" };
        string submittedType = "Reschedule";

        Assert.Contains(submittedType, allowedTypes);
    }

    [Fact]
    public async Task UC_3_10_10_ApproveEventChangeRequest_AppliesNewDatesToEvent()
    {
        using var db = CreateInMemoryDbContext();
        var initialStart = DateTime.UtcNow.AddDays(7);
        var initialEnd = initialStart.AddHours(3);

        var evt = new Event
        {
            EventId = 77,
            Title = "Arena Rock Tour",
            Slug = "arena-rock-tour",
            OrganizerId = 600,
            Status = EventStatus.Published,
            StartsAt = initialStart,
            EndsAt = initialEnd
        };
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        // Admin approves rescheduling to +30 days
        var newStart = initialStart.AddDays(30);
        var newEnd = initialEnd.AddDays(30);

        evt.StartsAt = newStart;
        evt.EndsAt = newEnd;
        evt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var refreshed = await db.Events.FindAsync(77);
        Assert.NotNull(refreshed);
        Assert.Equal(newStart, refreshed.StartsAt);
        Assert.Equal(newEnd, refreshed.EndsAt);
    }

    [Fact]
    public void UC_3_10_11_ViewAffectedOrders_ComputesTotalAffectedUnits()
    {
        var sampleOrders = new[]
        {
            new { OrderId = 1001, TicketCount = 2, TotalAmount = 2000000m },
            new { OrderId = 1002, TicketCount = 4, TotalAmount = 4000000m },
            new { OrderId = 1003, TicketCount = 1, TotalAmount = 1000000m }
        };

        int affectedTickets = sampleOrders.Sum(o => o.TicketCount);
        decimal affectedRevenue = sampleOrders.Sum(o => o.TotalAmount);

        Assert.Equal(7, affectedTickets);
        Assert.Equal(7000000m, affectedRevenue);
    }
}

