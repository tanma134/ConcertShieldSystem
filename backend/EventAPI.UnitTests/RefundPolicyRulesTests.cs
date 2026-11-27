using EventAPI.Common;
using Xunit;

namespace EventAPI.UnitTests;

public class RefundPolicyRulesTests
{
    private static readonly DateTime EventStart = new(2026, 12, 10, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WarnsWhenSalesLeadIsShorterThanDeadline()
    {
        var warning = RefundPolicyRules.GetSalesLeadWarning(EventStart.AddHours(-24), EventStart, 48);

        Assert.Equal(RefundPolicyRules.SalesLeadWarning, warning);
    }

    [Fact]
    public void DoesNotWarnWhenSalesLeadIsLongerThanDeadline()
    {
        var warning = RefundPolicyRules.GetSalesLeadWarning(EventStart.AddHours(-72), EventStart, 48);

        Assert.Null(warning);
    }

    [Fact]
    public void EqualBoundaryWarns()
    {
        var warning = RefundPolicyRules.GetSalesLeadWarning(EventStart.AddHours(-48), EventStart, 48);

        Assert.NotNull(warning);
    }

    [Fact]
    public void MissingSalesStartDoesNotWarn()
    {
        var warning = RefundPolicyRules.GetSalesLeadWarning(null, EventStart, 48);

        Assert.Null(warning);
    }

    [Fact]
    public void InvalidScheduleDoesNotCreateAFalseWarning()
    {
        var warning = RefundPolicyRules.GetSalesLeadWarning(EventStart.AddHours(1), EventStart, 48);

        Assert.Null(warning);
    }
}
