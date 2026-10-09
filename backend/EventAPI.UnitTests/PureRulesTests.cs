using EventAPI.Common;
using Xunit;

namespace EventAPI.UnitTests;

// Rules that need no database: status helpers, seating modes and slugs.
public class PureRulesTests
{
    [Fact]
    public void LegacyApprovedIsTreatedAsPublished()
    {
        Assert.Equal(EventStatus.Published, EventStatus.Normalize("Approved"));
        Assert.True(EventStatus.IsPublic("Approved"));
    }

    [Fact]
    public void OnlyDraftAndRejectedAreEditableAndSubmittable()
    {
        Assert.Equal(new[] { EventStatus.Draft, EventStatus.Rejected }, EventStatus.Editable);
        Assert.Equal(new[] { EventStatus.Draft, EventStatus.Rejected }, EventStatus.Submittable);
        Assert.False(EventStatus.IsPublic(EventStatus.Pending));
        Assert.False(EventStatus.IsPublic(EventStatus.Cancelled));
    }

    [Fact]
    public void UnknownStatusIsNotValid()
    {
        Assert.False(EventStatus.IsValid("Archived"));
        Assert.False(EventStatus.IsValid(null));
        Assert.True(EventStatus.IsValid("Draft"));
    }

    [Fact]
    public void SeatingModeDefaultsToReservedAndIgnoresCase()
    {
        Assert.Equal(SeatingMode.ReservedSeating, SeatingMode.Normalize(null));
        Assert.Equal(SeatingMode.StandingZones, SeatingMode.Normalize("standingzones"));
        Assert.True(SeatingMode.IsGeneralAdmission("GENERALADMISSION"));
        Assert.False(SeatingMode.IsValid("Mixed"));
    }

    [Fact]
    public void SlugsDropAccentsAndPunctuation()
    {
        Assert.Equal("nhac-can-tho", SlugHelper.NormalizeSlug("Nhạc Cần Thơ!"));
    }

    [Fact]
    public void SlugValidationRejectsBadShapes()
    {
        Assert.False(SlugHelper.IsValidSlug("ab", out _));
        Assert.False(SlugHelper.IsValidSlug("-abc", out _));
        Assert.False(SlugHelper.IsValidSlug("a--b", out _));
        Assert.True(SlugHelper.IsValidSlug("rock-night-2", out _));
    }
}
