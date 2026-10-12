using EventAPI.Common;
using Xunit;
namespace EventAPI.UnitTests;
public class PricingScheduleRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 13, 0, 30, DateTimeKind.Utc);
    [Theory]
    [InlineData(-1, 60)]
    [InlineData(0, 60)]
    [InlineData(10, 10)]
    [InlineData(10, 5)]
    [InlineData(10, 121)]
    public void InvalidMinuteOrRangeIsRejected(int from, int to)
        => Assert.Throws<InvalidOperationException>(() => PricingScheduleRules.Validate("EarlyBird",
            Now.AddMinutes(from), Now.AddMinutes(to), Now, Now.AddMinutes(120), Now.AddMinutes(180), Now, true));
    [Fact]
    public void FutureMinuteOnSameDayIsAccepted()
        => PricingScheduleRules.Validate("EarlyBird", Now.AddMinutes(1), Now.AddMinutes(60), Now,
            Now.AddMinutes(120), Now.AddMinutes(180), Now, true);
    [Fact]
    public void MissingEndIsRejected()
        => Assert.Throws<InvalidOperationException>(() => PricingScheduleRules.Validate("EarlyBird", Now.AddMinutes(1), null,
            null, null, Now.AddDays(1), Now, true));
    [Fact]
    public void BeforeSalesIsRejected()
        => Assert.Throws<InvalidOperationException>(() => PricingScheduleRules.Validate("EarlyBird", Now.AddMinutes(1), Now.AddMinutes(60),
            Now.AddMinutes(2), null, Now.AddDays(1), Now, true));
    [Fact]
    public void AfterConcertStartIsRejected()
        => Assert.Throws<InvalidOperationException>(() => PricingScheduleRules.Validate("EarlyBird", Now.AddMinutes(1), Now.AddMinutes(60),
            null, null, Now.AddMinutes(30), Now, true));
    [Fact]
    public void ExistingRuleCanBeDisabledWithoutMovingOldStartIntoFuture()
        => PricingScheduleRules.Validate("EarlyBird", Now.AddMinutes(-60), Now.AddMinutes(-30), null, null, Now.AddDays(1), Now, false);
    [Fact]
    public void QuantityRuleDoesNotNeedTimes()
        => PricingScheduleRules.Validate("QuantityBased", null, null, null, null, Now.AddDays(1), Now, true);
}
