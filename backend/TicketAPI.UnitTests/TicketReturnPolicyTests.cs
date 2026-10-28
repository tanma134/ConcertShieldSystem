using TicketAPI.Domain;
using Xunit;

namespace TicketAPI.UnitTests;

// UC_9.1 / UC_9.3: who may return a ticket, and who may cancel a return request.
public class TicketReturnPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc);

    // Builds a ticket that is eligible by default; each test breaks one rule.
    private static TicketReturnFacts Eligible(
        string ticketStatus = TicketStatuses.Active,
        string orderStatus = "Paid",
        DateTime? checkedInAt = null,
        DateTime? startsAt = null,
        bool hasOpenRequest = false)
    {
        return new TicketReturnFacts(
            ticketStatus,
            orderStatus,
            checkedInAt,
            startsAt ?? Now.AddDays(10),
            hasOpenRequest);
    }

    [Fact]
    public void ActivePaidTicketBeforeCutoffIsEligible()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(), Now);

        Assert.True(result.IsEligible);
        Assert.Equal("OK", result.Code);
    }

    [Fact]
    public void UnpaidOrderCannotBeReturned()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(orderStatus: "Pending"), Now);

        Assert.False(result.IsEligible);
        Assert.Equal("ORDER_NOT_PAID", result.Code);
    }

    [Fact]
    public void CheckedInTicketCannotBeReturned()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(checkedInAt: Now.AddDays(-1)), Now);

        Assert.False(result.IsEligible);
        Assert.Equal("ALREADY_CHECKED_IN", result.Code);
    }

    [Fact]
    public void TicketWithOpenRequestCannotBeReturnedTwice()
    {
        var result = TicketReturnPolicy.Evaluate(
            Eligible(ticketStatus: TicketStatuses.ReturnPending, hasOpenRequest: true), Now);

        Assert.False(result.IsEligible);
        Assert.Equal("RETURN_ALREADY_REQUESTED", result.Code);
    }

    [Fact]
    public void ReturnedTicketCannotBeReturnedAgain()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(ticketStatus: TicketStatuses.Returned), Now);

        Assert.False(result.IsEligible);
        Assert.Equal("ALREADY_RETURNED", result.Code);
    }

    [Fact]
    public void UnknownTicketStatusIsRejected()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(ticketStatus: "Revoked"), Now);

        Assert.False(result.IsEligible);
        Assert.Equal("TICKET_NOT_ACTIVE", result.Code);
    }

    [Fact]
    public void ReturnWindowClosesBeforeTheEventStarts()
    {
        // Default cutoff is 24 hours: 23 hours before the show is too late.
        var result = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddHours(23)), Now);

        Assert.False(result.IsEligible);
        Assert.Equal("RETURN_WINDOW_CLOSED", result.Code);
    }

    [Fact]
    public void ReturnIsAllowedExactlyAtTheCutoff()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddHours(24)), Now);

        Assert.True(result.IsEligible);
    }

    [Fact]
    public void CutoffHoursCanBeConfigured()
    {
        var facts = Eligible(startsAt: Now.AddHours(30));

        Assert.True(TicketReturnPolicy.Evaluate(facts, Now, cutoffHours: 24).IsEligible);
        Assert.False(TicketReturnPolicy.Evaluate(facts, Now, cutoffHours: 48).IsEligible);
    }

    [Fact]
    public void EventThatAlreadyStartedCannotBeReturned()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddHours(-2)), Now);

        Assert.False(result.IsEligible);
        Assert.Equal("RETURN_WINDOW_CLOSED", result.Code);
    }

    [Fact]
    public void ReasonIsRequiredAndTrimmed()
    {
        Assert.False(TicketReturnPolicy.ValidateReason(null).IsValid);
        Assert.False(TicketReturnPolicy.ValidateReason("   ").IsValid);
        Assert.False(TicketReturnPolicy.ValidateReason("too short").IsValid);
        Assert.True(TicketReturnPolicy.ValidateReason("  I can no longer attend  ").IsValid);
    }

    [Fact]
    public void ReasonLongerThanTheLimitIsRejected()
    {
        var tooLong = new string('a', TicketReturnPolicy.MaxReasonLength + 1);

        Assert.False(TicketReturnPolicy.ValidateReason(tooLong).IsValid);
    }

    [Fact]
    public void OnlyPendingRequestsCanBeCancelled()
    {
        Assert.True(TicketReturnPolicy.CanCancel(ReturnRequestStatuses.Pending));
        Assert.False(TicketReturnPolicy.CanCancel(ReturnRequestStatuses.Approved));
        Assert.False(TicketReturnPolicy.CanCancel(ReturnRequestStatuses.Rejected));
        Assert.False(TicketReturnPolicy.CanCancel(ReturnRequestStatuses.Cancelled));
        Assert.False(TicketReturnPolicy.CanCancel(null));
    }

    [Fact]
    public void RefundIsTheTicketPriceMinusItsShareOfTheOrderDiscount()
    {
        // Order of 3,500,000 with a 350,000 discount: a 1,000,000 ticket gets 100,000 off.
        Assert.Equal(900_000, TicketReturnPolicy.RefundAmount(1_000_000, 3_500_000, 350_000));
    }

    [Fact]
    public void RefundWithoutDiscountIsTheFullPrice()
    {
        Assert.Equal(500_000, TicketReturnPolicy.RefundAmount(500_000, 2_000_000, 0));
        Assert.Equal(500_000, TicketReturnPolicy.RefundAmount(500_000, 2_000_000, null));
    }

    [Fact]
    public void RefundNeverGoesBelowZero()
    {
        Assert.Equal(0, TicketReturnPolicy.RefundAmount(100, 100, 500));
        Assert.Equal(0, TicketReturnPolicy.RefundAmount(100, 0, 0) - 100);
    }

    // ---- Organizer refund tiers (EventAPI refund policies) ----

    // Typical organizer setup: 100% back until 7 days before, 50% until 2 days before.
    private static readonly RefundTier[] Tiers =
    {
        new RefundTier(168, 100m),
        new RefundTier(48, 50m)
    };

    [Fact]
    public void FarBeforeEventTheBestTierApplies()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddDays(10)), Now, Tiers);

        Assert.True(result.IsEligible);
        Assert.Equal(100m, result.RefundPercent);
    }

    [Fact]
    public void InsideTheFirstDeadlineTheLowerTierApplies()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddHours(100)), Now, Tiers);

        Assert.True(result.IsEligible);
        Assert.Equal(50m, result.RefundPercent);
    }

    [Fact]
    public void InsideTheLastDeadlineTheReturnWindowIsClosed()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddHours(47)), Now, Tiers);

        Assert.False(result.IsEligible);
        Assert.Equal("RETURN_WINDOW_CLOSED", result.Code);
    }

    [Fact]
    public void ExactlyOnTheDeadlineStillQualifiesForThatTier()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddHours(168)), Now, Tiers);

        Assert.True(result.IsEligible);
        Assert.Equal(100m, result.RefundPercent);
    }

    [Fact]
    public void NoConfiguredTiersFallsBackToTheDefaultWindowAndFullRefund()
    {
        var open = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddHours(30)), Now, Array.Empty<RefundTier>());
        var closed = TicketReturnPolicy.Evaluate(Eligible(startsAt: Now.AddHours(23)), Now, null);

        Assert.True(open.IsEligible);
        Assert.Equal(100m, open.RefundPercent);
        Assert.False(closed.IsEligible);
        Assert.Equal("RETURN_WINDOW_CLOSED", closed.Code);
    }

    [Fact]
    public void TicketRulesStillWinOverTheTiers()
    {
        var result = TicketReturnPolicy.Evaluate(Eligible(orderStatus: "Pending"), Now, Tiers);

        Assert.False(result.IsEligible);
        Assert.Equal("ORDER_NOT_PAID", result.Code);
    }

    [Fact]
    public void SelectTierReturnsNullWhenEveryDeadlinePassed()
    {
        Assert.Null(TicketReturnPolicy.SelectTier(Tiers, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void ApplyPercentRoundsDownToWholeDong()
    {
        Assert.Equal(166666L, TicketReturnPolicy.ApplyPercent(333333, 50m));
        Assert.Equal(500000L, TicketReturnPolicy.ApplyPercent(1000000, 50m));
        Assert.Equal(0L, TicketReturnPolicy.ApplyPercent(1000000, 0m));
    }

    [Fact]
    public void ApplyPercentNeverRefundsMoreThanWasPaid()
    {
        Assert.Equal(1000L, TicketReturnPolicy.ApplyPercent(1000, 250m));
        Assert.Equal(0L, TicketReturnPolicy.ApplyPercent(1000, -10m));
    }

    [Fact]
    public void RefundCombinesDiscountShareAndTierPercent()
    {
        // 2 x 500k = 1,000k gross, 100k discount -> 50k share per ticket -> 450k paid; 50% tier -> 225k.
        var paid = TicketReturnPolicy.RefundAmount(500000, 1000000, 100000);

        Assert.Equal(450000L, paid);
        Assert.Equal(225000L, TicketReturnPolicy.ApplyPercent(paid, 50m));
    }
}
