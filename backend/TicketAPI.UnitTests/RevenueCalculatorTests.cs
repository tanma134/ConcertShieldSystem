using TicketAPI.Domain;
using Xunit;

namespace TicketAPI.UnitTests;

// UC_13.1 / UC_13.2: revenue totals, filters and per-day / per-ticket-type breakdowns.
public class RevenueCalculatorTests
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private static DateTime Utc(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    // One order with a VIP (id 1) and a Standard (id 2) line, optionally discounted.
    private static OrderFact SampleOrder(int id, DateTime date, long discount = 0) =>
        new(id, date, discount, new[]
        {
            new OrderDetailFact(1, "VIP", 2, 1_000_000),
            new OrderDetailFact(2, "Standard", 3, 500_000),
        });

    [Fact]
    public void LinesCarryGrossAmountPerTicketType()
    {
        var lines = RevenueCalculator.BuildLines(new[] { SampleOrder(1, Utc(10, 1)) });

        Assert.Equal(2, lines.Count);
        Assert.Equal(2_000_000, lines[0].GrossAmount);
        Assert.Equal(1_500_000, lines[1].GrossAmount);
    }

    [Fact]
    public void OrderDiscountIsSplitProportionallyAndSumsExactly()
    {
        var lines = RevenueCalculator.BuildLines(new[] { SampleOrder(1, Utc(10, 1), discount: 350_000) });

        // 3.5M gross, 350k discount: 2.0M share is 200k, 1.5M share is 150k.
        Assert.Equal(200_000, lines[0].DiscountShare);
        Assert.Equal(150_000, lines[1].DiscountShare);
        Assert.Equal(350_000, lines.Sum(l => l.DiscountShare));
    }

    [Fact]
    public void RoundingRemainderGoesToTheLastLine()
    {
        var order = new OrderFact(1, Utc(10, 1), 100, new[]
        {
            new OrderDetailFact(1, "A", 1, 100),
            new OrderDetailFact(2, "B", 1, 100),
            new OrderDetailFact(3, "C", 1, 100),
        });

        var lines = RevenueCalculator.BuildLines(new[] { order });

        Assert.Equal(100, lines.Sum(l => l.DiscountShare));
    }

    [Fact]
    public void SummaryAddsUpGrossDiscountRefundAndNet()
    {
        var lines = RevenueCalculator.BuildLines(new[] { SampleOrder(1, Utc(10, 1), discount: 350_000) });
        var refunds = new[] { new RefundLine(Utc(10, 2), 1, 400_000) };

        var summary = RevenueCalculator.Summarize(lines, refunds, null, null, null, Vietnam);

        Assert.Equal(3_500_000, summary.GrossRevenue);
        Assert.Equal(350_000, summary.Discounts);
        Assert.Equal(400_000, summary.Refunded);
        Assert.Equal(2_750_000, summary.NetRevenue);
        Assert.Equal(1, summary.OrdersCount);
        Assert.Equal(5, summary.TicketsSold);
    }

    [Fact]
    public void DateRangeKeepsOnlyOrdersInsideTheInclusiveDays()
    {
        var lines = RevenueCalculator.BuildLines(new[]
        {
            SampleOrder(1, Utc(10, 1, 5)),
            SampleOrder(2, Utc(10, 5, 5)),
            SampleOrder(3, Utc(10, 9, 5)),
        });

        // 10/04 -> 10/06 in Vietnam time only contains order 2.
        var summary = RevenueCalculator.Summarize(
            lines, Array.Empty<RefundLine>(),
            new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 6), null, Vietnam);

        Assert.Equal(1, summary.OrdersCount);
        Assert.Equal(3_500_000, summary.GrossRevenue);
    }

    [Fact]
    public void TicketTypeFilterOnlyCountsThatType()
    {
        var lines = RevenueCalculator.BuildLines(new[] { SampleOrder(1, Utc(10, 1)) });

        var summary = RevenueCalculator.Summarize(lines, Array.Empty<RefundLine>(), null, null, 2, Vietnam);

        Assert.Equal(1_500_000, summary.GrossRevenue);
        Assert.Equal(3, summary.TicketsSold);
        Assert.Single(summary.ByTicketType);
        Assert.Equal("Standard", summary.ByTicketType[0].TicketTypeName);
    }

    [Fact]
    public void ByDayUsesLocalTimeAndIsSortedAscending()
    {
        // 18:00 UTC on 10/01 is already 01:00 on 10/02 in Vietnam.
        var lines = RevenueCalculator.BuildLines(new[]
        {
            SampleOrder(1, Utc(10, 1, 18)),
            SampleOrder(2, Utc(10, 1, 2)),
        });

        var summary = RevenueCalculator.Summarize(lines, Array.Empty<RefundLine>(), null, null, null, Vietnam);

        Assert.Equal(2, summary.ByDay.Count);
        Assert.Equal(new DateOnly(2026, 10, 1), summary.ByDay[0].Date);
        Assert.Equal(new DateOnly(2026, 10, 2), summary.ByDay[1].Date);
    }

    [Fact]
    public void RefundsAreSubtractedOnTheDayTheyWereApproved()
    {
        var lines = RevenueCalculator.BuildLines(new[] { SampleOrder(1, Utc(10, 1, 3)) });
        var refunds = new[] { new RefundLine(Utc(10, 3, 3), 1, 1_000_000) };

        var summary = RevenueCalculator.Summarize(lines, refunds, null, null, null, Vietnam);

        Assert.Equal(2_500_000, summary.NetRevenue);
        Assert.Equal(1_000_000, summary.ByDay.Single(d => d.Date == new DateOnly(2026, 10, 3)).Refunded);
    }

    [Fact]
    public void ByTicketTypeIsSortedByNetRevenueDescending()
    {
        var lines = RevenueCalculator.BuildLines(new[] { SampleOrder(1, Utc(10, 1)) });

        var summary = RevenueCalculator.Summarize(lines, Array.Empty<RefundLine>(), null, null, null, Vietnam);

        Assert.Equal("VIP", summary.ByTicketType[0].TicketTypeName);
        Assert.Equal("Standard", summary.ByTicketType[1].TicketTypeName);
    }

    [Fact]
    public void EmptyInputGivesZeroSummary()
    {
        var summary = RevenueCalculator.Summarize(
            Array.Empty<RevenueLine>(), Array.Empty<RefundLine>(), null, null, null, Vietnam);

        Assert.Equal(0, summary.NetRevenue);
        Assert.Equal(0, summary.OrdersCount);
        Assert.Empty(summary.ByDay);
        Assert.Empty(summary.ByTicketType);
    }

    [Fact]
    public void RangeValidationRejectsReversedAndHugeRanges()
    {
        Assert.False(RevenueCalculator.ValidateRange(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1)).IsValid);
        Assert.False(RevenueCalculator.ValidateRange(new DateOnly(2024, 1, 1), new DateOnly(2026, 1, 1)).IsValid);
        Assert.True(RevenueCalculator.ValidateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5)).IsValid);
        Assert.True(RevenueCalculator.ValidateRange(null, null).IsValid);
    }
}
