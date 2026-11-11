using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventAPI.UnitTests.Features;

/// <summary>
/// Feature 12: Real-time Check-in Monitoring & Staff Assignment
/// Tests for UC_3.13.3 (View Staff List), UC_3.13.4 (Assign/Unassigned Staff)
/// </summary>
public class Feature12_StaffAssignmentTests
{
    private EventDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    [Fact]
    public async Task UC_3_13_3_ViewStaffList_ReturnsAssignedStaffForEvent()
    {
        using var db = CreateInMemoryDbContext();
        db.EventStaff.AddRange(
            new EventStaff { EventStaffId = 1, EventId = 10, StaffUserId = 201, GateName = "Gate A", CanReviewReturns = false, AssignedBy = 501, AssignedAt = DateTime.UtcNow },
            new EventStaff { EventStaffId = 2, EventId = 10, StaffUserId = 202, GateName = "VIP Gate", CanReviewReturns = true, AssignedBy = 501, AssignedAt = DateTime.UtcNow },
            new EventStaff { EventStaffId = 3, EventId = 99, StaffUserId = 203, GateName = "Main Gate", CanReviewReturns = false, AssignedBy = 501, AssignedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var eventStaff = await db.EventStaff.Where(s => s.EventId == 10).ToListAsync();

        Assert.Equal(2, eventStaff.Count);
        Assert.Contains(eventStaff, s => s.StaffUserId == 201);
        Assert.Contains(eventStaff, s => s.StaffUserId == 202);
    }

    [Fact]
    public async Task UC_3_13_4_AssignUnassignedStaff_AddsAndRemovesStaffMember()
    {
        using var db = CreateInMemoryDbContext();
        var assignment = new EventStaff
        {
            EventStaffId = 10,
            EventId = 25,
            StaffUserId = 301,
            GateName = "Gate North",
            CanReviewReturns = false,
            AssignedBy = 501,
            AssignedAt = DateTime.UtcNow
        };
        db.EventStaff.Add(assignment);
        await db.SaveChangesAsync();

        // Check assigned
        var found = await db.EventStaff.FindAsync(10);
        Assert.NotNull(found);
        Assert.Equal("Gate North", found.GateName);

        // Unassign staff
        db.EventStaff.Remove(found);
        await db.SaveChangesAsync();

        var afterRemoval = await db.EventStaff.FindAsync(10);
        Assert.Null(afterRemoval);
    }
}
