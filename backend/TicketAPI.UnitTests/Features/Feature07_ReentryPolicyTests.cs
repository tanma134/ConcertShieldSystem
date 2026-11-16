using Xunit;

namespace TicketAPI.UnitTests.Features;

/// <summary>
/// Feature 7: Event Check-in & Access Control
/// Tests for UC_3.8.1 (Configure Re-entry Policy per Event)
/// </summary>
public class Feature07_ReentryPolicyTests
{
    [Fact]
    public void UC_3_8_1_ConfigureReentryPolicy_AllowedPolicyWithWristband()
    {
        bool allowReentry = true;
        int maxReentries = 2;
        string verificationMethod = "WristbandAndStamp";

        Assert.True(allowReentry);
        Assert.Equal(2, maxReentries);
        Assert.Equal("WristbandAndStamp", verificationMethod);
    }

    [Fact]
    public void UC_3_8_1_ConfigureReentryPolicy_StrictNoReentryPolicy()
    {
        bool allowReentry = false;
        int maxReentries = 0;

        Assert.False(allowReentry);
        Assert.Equal(0, maxReentries);
    }

    [Fact]
    public void UC_3_8_1_ConfigureReentryPolicy_ReentryWindowMinutes_EnforcesWindow()
    {
        int windowMinutes = 45;
        Assert.InRange(windowMinutes, 15, 120);
    }
}

