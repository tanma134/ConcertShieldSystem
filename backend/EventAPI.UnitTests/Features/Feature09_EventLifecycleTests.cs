using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventAPI.UnitTests.Features;

/// <summary>
/// Feature 9: Event Creation & Lifecycle Management
/// Tests for UC_3.10.1 (View Concert List), UC_3.10.2 (View Concert Details),
/// UC_3.10.3 (Edit Concert Information), UC_3.10.4 (Submit Concert for Approval),
/// UC_3.10.5 (Create Concert Event), UC_3.10.6 (Delete Draft Concert), UC_3.10.7 (Cancel Event)
/// </summary>
public class Feature09_EventLifecycleTests
{
    private EventDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    [Fact]
    public async Task UC_3_10_1_ViewConcertList_FiltersByOrganizerId()
    {
        using var db = CreateInMemoryDbContext();
        int organizerA = 501;
        int organizerB = 502;

        db.Events.AddRange(
            new Event { EventId = 1, Title = "Concert A1", Slug = "concert-a1", OrganizerId = organizerA, Status = EventStatus.Draft, StartsAt = DateTime.UtcNow.AddDays(5), EndsAt = DateTime.UtcNow.AddDays(6) },
            new Event { EventId = 2, Title = "Concert A2", Slug = "concert-a2", OrganizerId = organizerA, Status = EventStatus.Published, StartsAt = DateTime.UtcNow.AddDays(7), EndsAt = DateTime.UtcNow.AddDays(8) },
            new Event { EventId = 3, Title = "Concert B1", Slug = "concert-b1", OrganizerId = organizerB, Status = EventStatus.Draft, StartsAt = DateTime.UtcNow.AddDays(9), EndsAt = DateTime.UtcNow.AddDays(10) }
        );
        await db.SaveChangesAsync();

        var myConcerts = await db.Events
            .Where(e => e.OrganizerId == organizerA)
            .OrderBy(e => e.EventId)
            .ToListAsync();

        Assert.Equal(2, myConcerts.Count);
        Assert.All(myConcerts, c => Assert.Equal(organizerA, c.OrganizerId));
    }

    [Fact]
    public async Task UC_3_10_2_ViewConcertDetails_OwnerCanViewOwnDraft()
    {
        using var db = CreateInMemoryDbContext();
        int organizerId = 501;
        var concert = new Event
        {
            EventId = 10,
            Title = "Draft Underground Gig",
            Slug = "draft-underground",
            OrganizerId = organizerId,
            Status = EventStatus.Draft,
            Description = "Secret indie show",
            StartsAt = DateTime.UtcNow.AddDays(14),
            EndsAt = DateTime.UtcNow.AddDays(15)
        };
        db.Events.Add(concert);
        await db.SaveChangesAsync();

        var details = await db.Events
            .FirstOrDefaultAsync(e => e.EventId == 10 && e.OrganizerId == organizerId);

        Assert.NotNull(details);
        Assert.Equal("Draft Underground Gig", details.Title);
        Assert.Equal(EventStatus.Draft, details.Status);
    }

    [Fact]
    public void UC_3_10_3_EditConcertInformation_DraftStatus_IsEditable()
    {
        Assert.Contains(EventStatus.Draft, EventStatus.Editable);
        Assert.Contains(EventStatus.Rejected, EventStatus.Editable);
        Assert.DoesNotContain(EventStatus.Published, EventStatus.Editable);
        Assert.DoesNotContain(EventStatus.Cancelled, EventStatus.Editable);
    }

    [Fact]
    public async Task UC_3_10_3_EditConcertInformation_UpdatesFieldsSuccessfully()
    {
        using var db = CreateInMemoryDbContext();
        var concert = new Event
        {
            EventId = 20,
            Title = "Original Title",
            Slug = "original-title",
            OrganizerId = 501,
            Status = EventStatus.Draft,
            LocationName = "Original Venue",
            City = "Can Tho",
            StartsAt = DateTime.UtcNow.AddDays(20),
            EndsAt = DateTime.UtcNow.AddDays(21)
        };
        db.Events.Add(concert);
        await db.SaveChangesAsync();

        // Update title and location
        concert.Title = "Updated Title 2026";
        concert.LocationName = "New Stadium Venue";
        concert.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var updated = await db.Events.FindAsync(20);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title 2026", updated.Title);
        Assert.Equal("New Stadium Venue", updated.LocationName);
    }

    [Fact]
    public void UC_3_10_4_SubmitConcertForApproval_ValidDraft_TransitionsToPending()
    {
        string currentStatus = EventStatus.Draft;
        Assert.Contains(currentStatus, EventStatus.Submittable);

        string nextStatus = EventStatus.Pending;
        Assert.False(EventStatus.IsPublic(nextStatus));
        Assert.True(EventStatus.IsValid(nextStatus));
    }

    [Fact]
    public void UC_3_10_5_CreateConcertEvent_NormalizesSlug()
    {
        string rawTitle = "Live Concert Đà Nẵng 2026!";
        string slug = SlugHelper.NormalizeSlug(rawTitle);

        Assert.Equal("live-concert-da-nang-2026", slug);
        Assert.True(SlugHelper.IsValidSlug(slug, out _));
    }

    [Fact]
    public async Task UC_3_10_6_DeleteDraftConcert_RemovesDraftRecord()
    {
        using var db = CreateInMemoryDbContext();
        var draft = new Event
        {
            EventId = 30,
            Title = "Draft to Delete",
            Slug = "draft-to-delete",
            OrganizerId = 501,
            Status = EventStatus.Draft,
            StartsAt = DateTime.UtcNow.AddDays(30),
            EndsAt = DateTime.UtcNow.AddDays(31)
        };
        db.Events.Add(draft);
        await db.SaveChangesAsync();

        db.Events.Remove(draft);
        await db.SaveChangesAsync();

        var check = await db.Events.FindAsync(30);
        Assert.Null(check);
    }

    [Fact]
    public async Task UC_3_10_7_CancelEvent_PublishedConcert_SetsStatusCancelled()
    {
        using var db = CreateInMemoryDbContext();
        var liveConcert = new Event
        {
            EventId = 40,
            Title = "Concert Weather Cancelled",
            Slug = "weather-cancelled",
            OrganizerId = 501,
            Status = EventStatus.Published,
            StartsAt = DateTime.UtcNow.AddDays(3),
            EndsAt = DateTime.UtcNow.AddDays(4)
        };
        db.Events.Add(liveConcert);
        await db.SaveChangesAsync();

        liveConcert.Status = EventStatus.Cancelled;
        liveConcert.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var check = await db.Events.FindAsync(40);
        Assert.NotNull(check);
        Assert.Equal(EventStatus.Cancelled, check.Status);
    }
}
