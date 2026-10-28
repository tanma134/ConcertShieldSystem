using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using Xunit;
namespace EventAPI.UnitTests;
public class GovernanceRulesTests
{
    private static Event Published() => new() { Status = "Published", StartsAt = DateTime.UtcNow.AddDays(10), EndsAt = DateTime.UtcNow.AddDays(10).AddHours(2) };
    [Theory]
    [InlineData("Draft")][InlineData("Pending")][InlineData("Cancelled")][InlineData("Rejected")]
    public void InvalidLifecycleCannotChange(string status) { var ev = Published(); ev.Status = status; Assert.Throws<InvalidOperationException>(() => GovernanceRules.ValidateChange(ev,new("Postpone","Venue unavailable",null,null),DateTime.UtcNow)); }
    [Theory]
    [InlineData(0)][InlineData(9)][InlineData(1001)]
    public void InvalidReasonLengthFails(int length) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Published(),new("Postpone",new string('a',length),null,null),DateTime.UtcNow));
    [Fact] public void PostponeIgnoresDates() { var clean = GovernanceRules.ValidateChange(Published(),new("Postpone","Venue unavailable",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow),DateTime.UtcNow); Assert.Null(clean.NewStartsAt); Assert.Null(clean.NewEndsAt); }
    [Fact] public void SameScheduleFails() { var ev = Published(); Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateChange(ev,new("Reschedule","New schedule needed",ev.StartsAt,ev.EndsAt),DateTime.UtcNow)); }
    [Fact] public void PastStartFails() => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateChange(Published(),new("Reschedule","New schedule needed",DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1)),DateTime.UtcNow));
    [Fact] public void ReversedScheduleFails() => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateChange(Published(),new("Reschedule","New schedule needed",DateTimeOffset.UtcNow.AddDays(3),DateTimeOffset.UtcNow.AddDays(2)),DateTime.UtcNow));
    [Fact] public void PostponedCanReceiveNewSchedule() { var ev = Published(); ev.Status = "Postponed"; var clean = GovernanceRules.ValidateChange(ev,new("Reschedule","New schedule needed",DateTimeOffset.UtcNow.AddDays(3),DateTimeOffset.UtcNow.AddDays(4)),DateTime.UtcNow); Assert.Equal("Reschedule",clean.Type); }
    [Fact] public void StartedConcertFails() { var ev = Published(); ev.StartsAt=DateTime.UtcNow.AddMinutes(-1); Assert.Throws<InvalidOperationException>(()=>GovernanceRules.ValidateChange(ev,new("Postpone","Venue unavailable",null,null),DateTime.UtcNow)); }
    [Theory][InlineData("Approved")][InlineData("Rejected")][InlineData("RequestMoreInfo")]
    public void EveryComplianceDecisionRequiresNotes(string decision) => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateReview(decision,"  ",true));
    [Fact] public void UnknownDecisionFails() => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateReview("Other","Reviewed",true));
    [Fact] public void TrimmedReviewIsSaved() => Assert.Equal("Reviewed",GovernanceRules.ValidateReview("Approved"," Reviewed ",true));
    [Fact] public void OldVersionCannotPublish() => Assert.False(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus="Approved",ComplianceVersion=2,ComplianceReviewedVersion=1 }));
    [Fact] public void CurrentVersionCanPublish() => Assert.True(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus="Approved",ComplianceVersion=2,ComplianceReviewedVersion=2 }));
    [Fact] public void ZeroVersionCannotPublish() => Assert.False(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus="Approved",ComplianceVersion=0,ComplianceReviewedVersion=0 }));
    [Fact] public void PdfAccepted() => Assert.Equal("application/pdf",GovernanceRules.ValidateFile("permit.pdf","application/pdf","%PDF-1.7\n"u8.ToArray(),1024));
    [Fact] public void EmptyFileFails() => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateFile("permit.pdf","application/pdf",Array.Empty<byte>(),1024));
    [Fact] public void TooLargeFileFails() => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateFile("permit.pdf","application/pdf","%PDF-1.7"u8.ToArray(),2));
    [Fact] public void SpoofedPdfFails() => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateFile("permit.pdf","application/pdf","not a pdf"u8.ToArray(),1024));
    [Fact] public void WrongMimeFails() => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateFile("permit.pdf","image/png","%PDF-1.7"u8.ToArray(),1024));
    [Fact] public void WrongExtensionFails() => Assert.Throws<ArgumentException>(()=>GovernanceRules.ValidateFile("permit.exe","application/pdf","%PDF-1.7"u8.ToArray(),1024));
}
