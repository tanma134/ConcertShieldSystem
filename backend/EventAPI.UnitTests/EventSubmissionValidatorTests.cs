using EventAPI.Data;
using EventAPI.Models;
using EventAPI.Repositories;
using EventAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventAPI.UnitTests;

public class EventSubmissionValidatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmissionAllowsNoRefundPolicyAndNoCoordinates(bool invalidPolicy)
    {
        using var db = new EventDbContext(new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Events.Add(new Event
        {
            EventId = 1, Title = "Concert", Slug = "concert", Description = "Live music",
            LocationName = "Venue", Address = "1 Main Street", City = "Can Tho",
            StartsAt = DateTime.UtcNow.AddDays(30), EndsAt = DateTime.UtcNow.AddDays(30).AddHours(3)
        });
        db.TicketTypes.Add(new TicketType
        {
            TicketTypeId = 1, EventId = 1, TypeName = "General", Status = "Active",
            Quantity = 100, Price = 100000, MinPerOrder = 1, MaxPerOrder = 4
        });
        if (invalidPolicy)
            db.RefundPolicies.Add(new RefundPolicy
            {
                EventId = 1, PolicyName = "Invalid", IsActive = true,
                RefundPercent = 101, DeadlineBeforeEventHours = 24
            });
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Publication:RequirePoster"] = "false",
            // An old deployment setting must not force organizers to offer refunds.
            ["Publication:RequireRefundPolicy"] = "true",
            ["Compliance:RequiredTypes:0"] = "EventPermit"
        }).Build();
        var validator = new EventSubmissionValidator(new EventRepository(db), new TicketTypeRepository(db),
            new RefundPolicyRepository(db), new SeatingRepository(db), db, config);

        var result = await validator.ValidateAsync(1);

        Assert.DoesNotContain(result.Errors, e => e.Contains("At least one active refund policy"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("Latitude/Longitude") || w.Contains("No refund policy"));
        Assert.Contains(result.Warnings, w => w.Contains("no sales start set"));
        Assert.Contains(result.Warnings, w => w.Contains("no sales end set"));
        Assert.Contains(result.Errors, e => e.Contains("Compliance document required"));
        Assert.Equal(invalidPolicy, result.Errors.Any(e => e.Contains("RefundPercent")));
    }
}
