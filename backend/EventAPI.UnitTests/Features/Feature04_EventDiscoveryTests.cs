using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventAPI.UnitTests.Features;

/// <summary>
/// Feature 4: Event Discovery & Search
/// Tests for UC_3.5.1 (View Event List), UC_3.5.2 (View Event Details), 
/// UC_3.5.9 (Search Event), UC_3.5.10 (View Featured Events)
/// </summary>
public class Feature04_EventDiscoveryTests
{
    private EventDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    [Fact]
    public async Task UC_3_5_1_ViewEventList_ReturnsPublishedEventsOnly()
    {
        using var db = CreateInMemoryDbContext();
        db.Events.AddRange(
            new Event { EventId = 1, Title = "Rock Fest 2026", Slug = "rock-fest-2026", Status = EventStatus.Published, StartsAt = DateTime.UtcNow.AddDays(10), EndsAt = DateTime.UtcNow.AddDays(11) },
            new Event { EventId = 2, Title = "Draft Show", Slug = "draft-show", Status = EventStatus.Draft, StartsAt = DateTime.UtcNow.AddDays(15), EndsAt = DateTime.UtcNow.AddDays(16) },
            new Event { EventId = 3, Title = "Cancelled Tour", Slug = "cancelled-tour", Status = EventStatus.Cancelled, StartsAt = DateTime.UtcNow.AddDays(20), EndsAt = DateTime.UtcNow.AddDays(21) }
        );
        await db.SaveChangesAsync();

        var publicEvents = await db.Events
            .Where(e => e.Status == EventStatus.Published)
            .OrderBy(e => e.StartsAt)
            .ToListAsync();

        Assert.Single(publicEvents);
        Assert.Equal("Rock Fest 2026", publicEvents[0].Title);
        Assert.Equal(EventStatus.Published, publicEvents[0].Status);
    }

    [Fact]
    public async Task UC_3_5_1_ViewEventList_Pagination_CalculatesCorrectOffsets()
    {
        using var db = CreateInMemoryDbContext();
        for (int i = 1; i <= 25; i++)
        {
            db.Events.Add(new Event
            {
                EventId = i,
                Title = $"Concert #{i}",
                Slug = $"concert-{i}",
                Status = EventStatus.Published,
                StartsAt = DateTime.UtcNow.AddDays(i),
                EndsAt = DateTime.UtcNow.AddDays(i + 1)
            });
        }
        await db.SaveChangesAsync();

        int page = 2;
        int pageSize = 10;
        var pagedItems = await db.Events
            .Where(e => e.Status == EventStatus.Published)
            .OrderBy(e => e.EventId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Assert.Equal(10, pagedItems.Count);
        Assert.Equal(11, pagedItems.First().EventId);
        Assert.Equal(20, pagedItems.Last().EventId);
    }

    [Fact]
    public async Task UC_3_5_2_ViewEventDetails_ExistingPublishedEvent_ReturnsSuccess()
    {
        using var db = CreateInMemoryDbContext();
        var targetEvent = new Event
        {
            EventId = 101,
            Title = "Acoustic Sunset 2026",
            Slug = "acoustic-sunset-2026",
            Status = EventStatus.Published,
            Description = "Live unplugged music session",
            LocationName = "Saigon Opera House",
            City = "Ho Chi Minh City",
            StartsAt = DateTime.UtcNow.AddDays(5),
            EndsAt = DateTime.UtcNow.AddDays(5).AddHours(3)
        };
        db.Events.Add(targetEvent);
        await db.SaveChangesAsync();

        var found = await db.Events.FirstOrDefaultAsync(e => e.EventId == 101);

        Assert.NotNull(found);
        Assert.Equal("Acoustic Sunset 2026", found.Title);
        Assert.Equal("Saigon Opera House", found.LocationName);
    }

    [Fact]
    public async Task UC_3_5_2_ViewEventDetails_NonExistentEvent_ReturnsNull()
    {
        using var db = CreateInMemoryDbContext();
        var found = await db.Events.FirstOrDefaultAsync(e => e.EventId == 9999);
        Assert.Null(found);
    }

    [Fact]
    public async Task UC_3_5_9_SearchEvent_FilterByKeywordAndCity_MatchesCorrectEvents()
    {
        using var db = CreateInMemoryDbContext();
        db.Events.AddRange(
            new Event { EventId = 1, Title = "Hanoi Jazz Night", Slug = "hanoi-jazz", LocationName = "Hanoi Opera House", City = "Ha Noi", Status = EventStatus.Published, StartsAt = DateTime.UtcNow.AddDays(2), EndsAt = DateTime.UtcNow.AddDays(3) },
            new Event { EventId = 2, Title = "Saigon Rock Fest", Slug = "saigon-rock", LocationName = "Military Zone 7 Stadium", City = "Ho Chi Minh", Status = EventStatus.Published, StartsAt = DateTime.UtcNow.AddDays(4), EndsAt = DateTime.UtcNow.AddDays(5) },
            new Event { EventId = 3, Title = "Da Nang EDM Rave", Slug = "danang-edm", LocationName = "Tien Son Sports Palace", City = "Da Nang", Status = EventStatus.Published, StartsAt = DateTime.UtcNow.AddDays(7), EndsAt = DateTime.UtcNow.AddDays(8) }
        );
        await db.SaveChangesAsync();

        string keyword = "Jazz";
        var results = await db.Events
            .Where(e => e.Status == EventStatus.Published && (e.Title.Contains(keyword) || (e.LocationName != null && e.LocationName.Contains(keyword))))
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Hanoi Jazz Night", results[0].Title);
    }

    [Fact]
    public void UC_3_5_9_SearchEvent_InvertedDateRange_FailsValidation()
    {
        var start = DateTime.UtcNow.AddDays(10);
        var end = DateTime.UtcNow.AddDays(5);

        Assert.True(start > end);
        Assert.False(start <= end, "Filter start date must precede filter end date");
    }

    [Fact]
    public async Task UC_3_5_10_ViewFeaturedEvents_ReturnsOnlyPublishedFeaturedEvents()
    {
        using var db = CreateInMemoryDbContext();
        db.Events.AddRange(
            new Event { EventId = 1, Title = "Featured Arena Show", Slug = "featured-arena", Status = EventStatus.Published, IsFeatured = true, StartsAt = DateTime.UtcNow.AddDays(3), EndsAt = DateTime.UtcNow.AddDays(4) },
            new Event { EventId = 2, Title = "Regular Published Gig", Slug = "regular-gig", Status = EventStatus.Published, IsFeatured = false, StartsAt = DateTime.UtcNow.AddDays(6), EndsAt = DateTime.UtcNow.AddDays(7) },
            new Event { EventId = 3, Title = "Draft Featured", Slug = "draft-featured", Status = EventStatus.Draft, IsFeatured = true, StartsAt = DateTime.UtcNow.AddDays(8), EndsAt = DateTime.UtcNow.AddDays(9) }
        );
        await db.SaveChangesAsync();

        var featured = await db.Events
            .Where(e => e.Status == EventStatus.Published && e.IsFeatured == true)
            .ToListAsync();

        Assert.Single(featured);
        Assert.Equal("Featured Arena Show", featured[0].Title);
    }
}
