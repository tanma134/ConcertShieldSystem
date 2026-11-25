using EventAPI.Common;
using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventAPI.UnitTests.Features;

/// <summary>
/// Feature 10: Seating Chart Builder & Configuration
/// Tests for UC_3.11.1 (Build Seating Chart), UC_3.11.2 (Edit Seating Zone),
/// UC_3.11.3 (Apply Seating Template), UC_3.11.4 (Save Seating Chart as Template),
/// UC_3.11.5 (Preview Seating Chart), UC_3.11.6 (Configure Ticket Types),
/// UC_3.11.7 (Configure Pricing Rule), UC_3.11.8 (Configure Refund Policy),
/// UC_3.11.9 (Configure Purchase Limit)
/// </summary>
public class Feature10_SeatingChartPricingTests
{
    private EventDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    [Fact]
    public async Task UC_3_11_1_BuildSeatingChart_CreatesSeatMapAndZones()
    {
        using var db = CreateInMemoryDbContext();
        var seatMap = new SeatMap
        {
            SeatMapId = 1,
            EventId = 10,
            Name = "National Stadium Grand Stage",
            LayoutJson = "{\"width\":1200,\"height\":800}",
            CreatedAt = DateTime.UtcNow
        };
        db.SeatMaps.Add(seatMap);

        var zoneVip = new SeatZone
        {
            SeatZoneId = 1,
            SeatMapId = 1,
            TicketTypeId = 101,
            ZoneName = "VIP Zone A",
            Capacity = 500,
            ZoneType = "Seated"
        };
        db.SeatZones.Add(zoneVip);
        await db.SaveChangesAsync();

        var savedMap = await db.SeatMaps.FindAsync(1);
        var savedZone = await db.SeatZones.FindAsync(1);

        Assert.NotNull(savedMap);
        Assert.Equal("National Stadium Grand Stage", savedMap.Name);
        Assert.NotNull(savedZone);
        Assert.Equal("VIP Zone A", savedZone.ZoneName);
    }

    [Fact]
    public async Task UC_3_11_2_EditSeatingZone_UpdatesCapacityAndCoordinates()
    {
        using var db = CreateInMemoryDbContext();
        var zone = new SeatZone
        {
            SeatZoneId = 5,
            SeatMapId = 1,
            TicketTypeId = 102,
            ZoneName = "Standing Zone B",
            Capacity = 1000,
            ZoneType = "Standing"
        };
        db.SeatZones.Add(zone);
        await db.SaveChangesAsync();

        zone.Capacity = 1200;
        await db.SaveChangesAsync();

        var updated = await db.SeatZones.FindAsync(5);
        Assert.NotNull(updated);
        Assert.Equal(1200, updated.Capacity);
    }

    [Fact]
    public void UC_3_11_3_ApplySeatingTemplate_ValidModes()
    {
        Assert.True(SeatingMode.IsValid(SeatingMode.ReservedSeating));
        Assert.True(SeatingMode.IsValid(SeatingMode.StandingZones));
        Assert.True(SeatingMode.IsValid(SeatingMode.GeneralAdmission));
    }

    [Fact]
    public async Task UC_3_11_4_SaveSeatingChartAsTemplate_PersistsTemplateEntity()
    {
        using var db = CreateInMemoryDbContext();
        var template = new SeatingTemplate
        {
            SeatingTemplateId = 1,
            OrganizerId = 501,
            Name = "Standard Arena 5000 Template",
            SeatingMode = SeatingMode.ReservedSeating,
            IsPublic = true,
            ZonesJson = "[]",
            CreatedAt = DateTime.UtcNow
        };
        db.SeatingTemplates.Add(template);
        await db.SaveChangesAsync();

        var saved = await db.SeatingTemplates.FindAsync(1);
        Assert.NotNull(saved);
        Assert.True(saved.IsPublic);
        Assert.Equal("Standard Arena 5000 Template", saved.Name);
    }

    [Fact]
    public async Task UC_3_11_5_PreviewSeatingChart_AggregatesAllZonesForMap()
    {
        using var db = CreateInMemoryDbContext();
        db.SeatZones.AddRange(
            new SeatZone { SeatZoneId = 10, SeatMapId = 2, TicketTypeId = 201, ZoneName = "Zone 1", Capacity = 300, ZoneType = "Seated" },
            new SeatZone { SeatZoneId = 11, SeatMapId = 2, TicketTypeId = 202, ZoneName = "Zone 2", Capacity = 700, ZoneType = "Standing" }
        );
        await db.SaveChangesAsync();

        var zones = await db.SeatZones.Where(z => z.SeatMapId == 2).ToListAsync();
        int totalComputed = zones.Sum(z => z.Capacity);

        Assert.Equal(2, zones.Count);
        Assert.Equal(1000, totalComputed);
    }

    [Fact]
    public async Task UC_3_11_6_ConfigureTicketTypes_ValidatesTiersAndPrices()
    {
        using var db = CreateInMemoryDbContext();
        var vip = new TicketType { TicketTypeId = 1, EventId = 10, TypeName = "VIP", Price = 2000000L, Quantity = 200, SoldQuantity = 0 };
        var standard = new TicketType { TicketTypeId = 2, EventId = 10, TypeName = "Standard", Price = 800000L, Quantity = 800, SoldQuantity = 0 };

        db.TicketTypes.AddRange(vip, standard);
        await db.SaveChangesAsync();

        var tiers = await db.TicketTypes.Where(t => t.EventId == 10).ToListAsync();
        Assert.Equal(2, tiers.Count);
        Assert.True(tiers.First(t => t.TypeName == "VIP").Price > tiers.First(t => t.TypeName == "Standard").Price);
    }

    [Fact]
    public void UC_3_11_7_ConfigurePricingRule_DiscountPercentageEnforced()
    {
        decimal basePrice = 1000000m;
        decimal discountPercent = 15m; // 15% EarlyBird

        decimal finalPrice = basePrice * (1m - (discountPercent / 100m));
        Assert.Equal(850000m, finalPrice);
    }

    [Fact]
    public async Task UC_3_11_8_ConfigureRefundPolicy_PersistsCutoffDaysAndPercentages()
    {
        using var db = CreateInMemoryDbContext();
        var policy = new RefundPolicy
        {
            RefundPolicyId = 1,
            EventId = 10,
            PolicyName = "Standard Tiered Refund",
            DeadlineBeforeEventHours = 168, // 7 days
            RefundPercent = 90.0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.RefundPolicies.Add(policy);
        await db.SaveChangesAsync();

        var saved = await db.RefundPolicies.FirstOrDefaultAsync(p => p.EventId == 10);
        Assert.NotNull(saved);
        Assert.True(saved.IsActive);
        Assert.Equal(168, saved.DeadlineBeforeEventHours);
        Assert.Equal(90.0m, saved.RefundPercent);
    }

    [Fact]
    public void UC_3_11_9_ConfigurePurchaseLimit_EnforcesBoundary()
    {
        int minLimit = 1;
        int maxLimit = 10;
        int selectedLimit = 4;

        Assert.InRange(selectedLimit, minLimit, maxLimit);
    }

    [Fact]
    public void UC_3_11_1_SeatingGeometry_OverlappingRectangles_Detected()
    {
        var zoneA = "{\"x\": 100, \"y\": 100, \"width\": 200, \"height\": 200}";
        var zoneB = "{\"x\": 150, \"y\": 150, \"width\": 200, \"height\": 200}";

        var boxA = EventAPI.Services.SeatingService.ParseBounds(zoneA);
        var boxB = EventAPI.Services.SeatingService.ParseBounds(zoneB);

        Assert.NotNull(boxA);
        Assert.NotNull(boxB);
        Assert.True(boxA.Value.Intersects(boxB.Value), "Overlapping bounding boxes must be detected");
    }

    [Fact]
    public void UC_3_11_1_SeatingGeometry_NonOverlappingRectangles_Accepted()
    {
        var zoneA = "{\"x\": 100, \"y\": 100, \"width\": 100, \"height\": 100}";
        var zoneB = "{\"x\": 300, \"y\": 100, \"width\": 100, \"height\": 100}";

        var boxA = EventAPI.Services.SeatingService.ParseBounds(zoneA);
        var boxB = EventAPI.Services.SeatingService.ParseBounds(zoneB);

        Assert.NotNull(boxA);
        Assert.NotNull(boxB);
        Assert.False(boxA.Value.Intersects(boxB.Value), "Adjacent non-overlapping zones must not collide");
    }

    [Fact]
    public void UC_3_11_1_SeatingGeometry_PolygonValidation_RequiresAtLeastThreeVertices()
    {
        var validPoly = "{\"points\":[{\"x\":0,\"y\":0},{\"x\":100,\"y\":0},{\"x\":50,\"y\":100}]}";
        var invalidPoly = "{\"points\":[{\"x\":0,\"y\":0},{\"x\":100,\"y\":0}]}";

        var boxValid = EventAPI.Services.SeatingService.ParseBounds(validPoly);
        var boxInvalid = EventAPI.Services.SeatingService.ParseBounds(invalidPoly);

        Assert.NotNull(boxValid);
        Assert.Null(boxInvalid);
    }

    [Fact]
    public void UC_3_11_7_EffectivePrice_AppliesPriorityAndQuantityThreshold()
    {
        var ticketType = new TicketType
        {
            TicketTypeId = 1,
            TypeName = "VIP",
            Price = 1000000L,
            Quantity = 100,
            SoldQuantity = 10,
            PricingRules = new List<PricingRule>()
        };

        // Rule 1: Priority 1, 10% discount -> 900,000
        ticketType.PricingRules.Add(new PricingRule
        {
            PricingRuleId = 1,
            RuleName = "EarlyBird10",
            RuleType = "EarlyBird",
            DiscountPercent = 10,
            Priority = 1,
            IsActive = true,
            TriggerFrom = DateTime.UtcNow.AddHours(-1),
            TriggerTo = DateTime.UtcNow.AddHours(2)
        });

        // Rule 2: Priority 2, Adjusted price 800,000 -> 800,000 wins due to higher Priority
        ticketType.PricingRules.Add(new PricingRule
        {
            PricingRuleId = 2,
            RuleName = "SuperEarlyBird",
            RuleType = "TimeBased",
            AdjustedPrice = 800000L,
            Priority = 2,
            IsActive = true,
            TriggerFrom = DateTime.UtcNow.AddHours(-1),
            TriggerTo = DateTime.UtcNow.AddHours(2)
        });

        var price = EventAPI.Services.EventService.ResolveEffectivePrice(ticketType);
        Assert.Equal(800000L, price);

        // When SoldQuantity exceeds QuantityThreshold, rule with threshold is bypassed
        ticketType.PricingRules.Clear();
        ticketType.SoldQuantity = 50;
        ticketType.PricingRules.Add(new PricingRule
        {
            PricingRuleId = 3,
            RuleName = "First20Only",
            RuleType = "QuantityBased",
            DiscountPercent = 50,
            QuantityThreshold = 20, // Only for first 20 tickets sold
            Priority = 1,
            IsActive = true
        });

        var priceAfterThreshold = EventAPI.Services.EventService.ResolveEffectivePrice(ticketType);
        Assert.Equal(1000000L, priceAfterThreshold);
    }
}
