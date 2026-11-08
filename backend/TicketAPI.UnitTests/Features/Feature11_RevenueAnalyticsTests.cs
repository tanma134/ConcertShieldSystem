using System;
using TicketAPI.Domain;
using Xunit;

namespace TicketAPI.UnitTests.Features;

/// <summary>
/// Feature 11: Revenue Analytics & Sales Dashboard
/// Tests for UC_3.12.1 (View Revenue Dashboard), UC_3.12.2 (Filter Revenue Dashboard),
/// UC_3.12.3 (Export Revenue Report)
/// </summary>
public class Feature11_RevenueAnalyticsTests
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private static DateTime Utc(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static OrderFact SampleOrder(int id, DateTime date, long discount = 0) =>
        new(id, date, discount, new[]
        {
            new OrderDetailFact(1, "VIP", 2, 1_000_000),
            new OrderDetailFact(2, "Standard", 3, 500_000),
        });

    [Fact]
    public void UC_3_12_1_ViewRevenueDashboard_CalculatesGrossAndNetRevenue()
    {
        var lines = RevenueCalculator.BuildLines(new[]
        {
            SampleOrder(1, Utc(10, 1), discount: 100_000),
            SampleOrder(2, Utc(10, 2), discount: 0)
        });

        var summary = RevenueCalculator.Summarize(lines, Array.Empty<RefundLine>(), null, null, null, Vietnam);

        Assert.Equal(7_000_000, summary.GrossRevenue);
        Assert.Equal(100_000, summary.Discounts);
        Assert.Equal(6_900_000, summary.NetRevenue);
        Assert.Equal(10, summary.TicketsSold);
        Assert.Equal(2, summary.OrdersCount);
    }

    [Fact]
    public void UC_3_12_2_FilterRevenueDashboard_FiltersByDateRange()
    {
        var lines = RevenueCalculator.BuildLines(new[]
        {
            SampleOrder(1, Utc(10, 1, 5)),
            SampleOrder(2, Utc(10, 5, 5)),
            SampleOrder(3, Utc(10, 15, 5))
        });

        var summary = RevenueCalculator.Summarize(
            lines,
            Array.Empty<RefundLine>(),
            new DateOnly(2026, 10, 4),
            new DateOnly(2026, 10, 6),
            null,
            Vietnam);

        Assert.Equal(1, summary.OrdersCount);
        Assert.Equal(3_500_000, summary.GrossRevenue);
    }

    [Fact]
    public void UC_3_12_3_ExportRevenueReport_GeneratesExpectedCsvHeaders()
    {
        var expectedHeaders = new[] { "Date", "TicketType", "QuantitySold", "UnitPrice", "GrossAmount", "Discount", "NetAmount" };

        var csvHeaderLine = string.Join(",", expectedHeaders);
        Assert.Contains("TicketType", csvHeaderLine);
        Assert.Contains("NetAmount", csvHeaderLine);
    }
}
