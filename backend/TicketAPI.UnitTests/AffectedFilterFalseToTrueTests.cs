using TicketAPI.Services;
using Xunit;
namespace TicketAPI.UnitTests;

// Test cases FALSE -> TRUE cho: View Affected Orders/Tickets by Change (bo loc va phan trang).
public class AffectedFilterFalseToTrueTests
{
    // ---------- FALSE ----------
    [Theory][InlineData("")][InlineData("order")][InlineData("Orders")][InlineData("ticket")]
    public void F01_UnknownTabIsRefused(string tab) => Assert.Throws<ArgumentException>(() => AffectedFilterRules.Validate(tab, null, null, null, 1, 20));
    [Theory][InlineData(0)][InlineData(-1)]
    public void F02_PageBelowOneIsRefused(int page) => Assert.Throws<ArgumentException>(() => AffectedFilterRules.Validate("orders", null, null, null, page, 20));
    [Theory][InlineData(0)][InlineData(-5)][InlineData(101)]
    public void F03_PageSizeOutOfRangeIsRefused(int size) => Assert.Throws<ArgumentException>(() => AffectedFilterRules.Validate("tickets", null, null, null, 1, size));
    [Theory][InlineData(0)][InlineData(-3)]
    public void F04_NonPositiveUserIdIsRefused(int user) => Assert.Throws<ArgumentException>(() => AffectedFilterRules.Validate("orders", null, null, user, 1, 20));
    [Theory][InlineData("paid")][InlineData("Cancelled")][InlineData("x")]
    public void F05_UnknownProcessingStatusIsRefused(string status) => Assert.Throws<ArgumentException>(() => AffectedFilterRules.Validate("orders", status, null, null, 1, 20));
    [Theory][InlineData("ORD-")][InlineData("ORD-0")][InlineData("0")][InlineData("-4")][InlineData("12abc")][InlineData("ord-5")][InlineData("ORD-ORD-5")][InlineData("99999999999")]
    public void F06_MalformedOrderCodeIsRefused(string code) => Assert.Throws<ArgumentException>(() => AffectedFilterRules.Validate("orders", null, code, null, 1, 20));

    // ---------- TRUE ----------
    [Fact] public void T01_NoFilterIsAccepted() => Assert.Null(AffectedFilterRules.Validate("orders", null, null, null, 1, 20));
    [Theory][InlineData("orders")][InlineData("tickets")]
    public void T02_BothTabsAreAccepted(string tab) => Assert.Null(AffectedFilterRules.Validate(tab, "", "", null, 1, 20));
    [Theory][InlineData(1)][InlineData(100)]
    public void T03_PageSizeBoundsAreAccepted(int size) => Assert.Null(AffectedFilterRules.Validate("orders", null, null, null, 1, size));
    [Theory][InlineData("Pending")][InlineData("Notified")][InlineData("RefundPending")][InlineData("Refunded")][InlineData("RefundFailed")][InlineData("Returned")][InlineData("Active")]
    public void T04_EveryKnownProcessingStatusIsAccepted(string status) => Assert.Null(AffectedFilterRules.Validate("tickets", status, null, null, 1, 20));
    [Fact] public void T05_PlainNumberOrderCodeIsParsed() => Assert.Equal(15, AffectedFilterRules.Validate("orders", null, "15", null, 1, 20));
    [Fact] public void T06_PrefixedOrderCodeIsParsed() => Assert.Equal(15, AffectedFilterRules.Validate("orders", null, "ORD-15", null, 1, 20));
    [Fact] public void T07_PositiveUserIdIsAccepted() => Assert.Null(AffectedFilterRules.Validate("orders", null, null, 7, 3, 50));
    [Fact] public void T08_AllFiltersTogetherAreAccepted() => Assert.Equal(9, AffectedFilterRules.Validate("tickets", "Refunded", "ORD-9", 2, 2, 10));
}
