using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using Xunit;
namespace EventAPI.UnitTests;

// Test cases FALSE -> TRUE cho: Postpone/Reschedule Event, Submit Event Compliance Documents,
// Review Event Compliance Record. Moi nhom di tu cac truong hop bi tu choi (FALSE) den truong hop thanh cong (TRUE).
public class PostponeRescheduleFalseToTrueTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc);
    private static Event Concert(string status = "Published") => new() { Status = status, StartsAt = Now.AddDays(30), EndsAt = Now.AddDays(30).AddHours(3) };
    private static ChangeInput Resched(DateTime s, DateTime e, string reason = "Venue is unavailable") => new("Reschedule", reason, new DateTimeOffset(s, TimeSpan.Zero), new DateTimeOffset(e, TimeSpan.Zero));

    // ---------- FALSE ----------
    [Theory][InlineData("Draft")][InlineData("Pending")][InlineData("Rejected")][InlineData("Cancelled")]
    public void F01_NonPublishedLifecycleIsRefused(string status) => Assert.Throws<InvalidOperationException>(() => GovernanceRules.ValidateChange(Concert(status), new("Postpone", "Venue is unavailable", null, null), Now));
    [Fact] public void F02_StartedPublishedConcertIsRefused() { var ev = Concert(); ev.StartsAt = Now.AddMinutes(-1); Assert.Throws<InvalidOperationException>(() => GovernanceRules.ValidateChange(ev, new("Postpone", "Venue is unavailable", null, null), Now)); }
    [Fact] public void F03_ConcertStartingExactlyNowIsRefused() { var ev = Concert(); ev.StartsAt = Now; Assert.Throws<InvalidOperationException>(() => GovernanceRules.ValidateChange(ev, new("Postpone", "Venue is unavailable", null, null), Now)); }
    [Theory][InlineData("")][InlineData("Cancel")][InlineData("postpone")][InlineData("Move")]
    public void F04_UnknownChangeTypeIsRefused(string type) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), new(type, "Venue is unavailable", null, null), Now));
    [Theory][InlineData(0)][InlineData(9)][InlineData(1001)]
    public void F05_ReasonLengthOutOfRangeIsRefused(int len) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), new("Postpone", new string('a', len), null, null), Now));
    [Fact] public void F06_ReasonOfOnlySpacesIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), new("Postpone", "            ", null, null), Now));
    [Fact] public void F07_NullReasonIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), new("Postpone", null!, null, null), Now));
    [Fact] public void F08_RescheduleWithoutDatesIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), new("Reschedule", "Venue is unavailable", null, null), Now));
    [Fact] public void F09_RescheduleWithoutEndIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), new("Reschedule", "Venue is unavailable", new DateTimeOffset(Now.AddDays(40), TimeSpan.Zero), null), Now));
    [Fact] public void F10_RescheduleInThePastIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), Resched(Now.AddDays(-2), Now.AddDays(-1)), Now));
    [Fact] public void F11_RescheduleStartingNowIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), Resched(Now, Now.AddHours(2)), Now));
    [Fact] public void F12_EndBeforeStartIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), Resched(Now.AddDays(5), Now.AddDays(4)), Now));
    [Fact] public void F13_EndEqualToStartIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(Concert(), Resched(Now.AddDays(5), Now.AddDays(5)), Now));
    [Fact] public void F14_UnchangedScheduleIsRefused() { var ev = Concert(); Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChange(ev, Resched(ev.StartsAt, ev.EndsAt), Now)); }
    [Fact] public void F15_PostponedConcertCannotBePostponedAgain() => Assert.Throws<InvalidOperationException>(() => GovernanceRules.ValidateChange(Concert("Postponed"), new("Postpone", "Venue is unavailable", null, null), Now));

    // ---------- TRUE ----------
    [Fact] public void T01_PostponePublishedConcertIsAccepted() { var c = GovernanceRules.ValidateChange(Concert(), new("Postpone", "Venue is unavailable", null, null), Now); Assert.Equal("Postpone", c.Type); }
    [Fact] public void T02_PostponeDropsAnyDatesAndTrimsReason() { var c = GovernanceRules.ValidateChange(Concert(), new("Postpone", "  Venue is unavailable  ", new DateTimeOffset(Now.AddDays(3), TimeSpan.Zero), new DateTimeOffset(Now.AddDays(4), TimeSpan.Zero)), Now); Assert.Null(c.NewStartsAt); Assert.Null(c.NewEndsAt); Assert.Equal("Venue is unavailable", c.Reason); }
    [Theory][InlineData(10)][InlineData(1000)]
    public void T03_ReasonBoundariesAreAccepted(int len) => Assert.Equal("Postpone", GovernanceRules.ValidateChange(Concert(), new("Postpone", new string('a', len), null, null), Now).Type);
    [Fact] public void T04_RescheduleToFutureWindowIsAccepted() { var c = GovernanceRules.ValidateChange(Concert(), Resched(Now.AddDays(45), Now.AddDays(45).AddHours(3)), Now); Assert.Equal("Reschedule", c.Type); Assert.Equal(Now.AddDays(45), c.NewStartsAt!.Value.UtcDateTime); }
    [Fact] public void T05_RescheduleToAnEarlierFutureDateIsAccepted() => Assert.Equal("Reschedule", GovernanceRules.ValidateChange(Concert(), Resched(Now.AddDays(2), Now.AddDays(2).AddHours(2)), Now).Type);
    [Fact] public void T06_OnlyTheEndMovingIsAChange() { var ev = Concert(); Assert.Equal("Reschedule", GovernanceRules.ValidateChange(ev, Resched(ev.StartsAt, ev.EndsAt.AddHours(1)), Now).Type); }
    [Fact] public void T07_PostponedConcertCanReceiveNewSchedule() => Assert.Equal("Reschedule", GovernanceRules.ValidateChange(Concert("Postponed"), Resched(Now.AddDays(60), Now.AddDays(60).AddHours(3)), Now).Type);
    [Fact] public void T08_PostponedConcertWithPastOldDateStillAcceptsFutureSchedule() { var ev = Concert("Postponed"); ev.StartsAt = Now.AddDays(-5); ev.EndsAt = Now.AddDays(-5).AddHours(3); Assert.Equal("Reschedule", GovernanceRules.ValidateChange(ev, Resched(Now.AddDays(20), Now.AddDays(20).AddHours(3)), Now).Type); }
}

public class SubmitComplianceDocumentsFalseToTrueTests
{
    private static readonly string[] Required = { "EventPermit", "SafetyPlan" };
    private static readonly byte[] Pdf = "%PDF-1.7\n1 0 obj"u8.ToArray();
    private static readonly byte[] Png = { 137, 80, 78, 71, 13, 10, 26, 10, 0 };
    private static readonly byte[] Jpg = { 255, 216, 255, 224, 0 };

    // ---------- FALSE: file ----------
    [Fact] public void F01_EmptyFileIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateFile("a.pdf", "application/pdf", Array.Empty<byte>(), 1024));
    [Fact] public void F02_FileOverTheLimitIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateFile("a.pdf", "application/pdf", new byte[1025].Select((_, i) => i < 5 ? "%PDF-"u8[i] : (byte)0).ToArray(), 1024));
    [Fact] public void F03_PdfExtensionWithTextContentIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateFile("a.pdf", "application/pdf", "hello world"u8.ToArray(), 1024));
    [Fact] public void F04_PdfBytesWithPngMimeIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateFile("a.pdf", "image/png", Pdf, 1024));
    [Fact] public void F05_PngBytesNamedPdfIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateFile("a.pdf", "application/pdf", Png, 1024));
    [Fact] public void F06_JpegBytesNamedPngIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateFile("a.png", "image/png", Jpg, 1024));
    [Theory][InlineData("a.exe")][InlineData("a.docx")][InlineData("a.gif")][InlineData("a")]
    public void F07_UnsupportedExtensionIsRefused(string name) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateFile(name, "application/pdf", Pdf, 1024));
    [Fact] public void F08_TruncatedPdfSignatureIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateFile("a.pdf", "application/pdf", "%PDF"u8.ToArray(), 1024));

    // ---------- FALSE: completeness before submit ----------
    [Fact] public void F09_NothingUploadedIsIncomplete() => Assert.False(GovernanceRules.IsComplete(0, Required, Array.Empty<string>()));
    [Fact] public void F10_OnlyPermitIsIncomplete() => Assert.False(GovernanceRules.IsComplete(1, Required, new[] { "EventPermit" }));
    [Fact] public void F11_OnlySafetyPlanIsIncomplete() => Assert.False(GovernanceRules.IsComplete(1, Required, new[] { "SafetyPlan" }));
    [Fact] public void F12_UnknownTypeDoesNotReplaceARequiredOne() => Assert.False(GovernanceRules.IsComplete(1, Required, new[] { "EventPermit", "Other" }));
    [Fact] public void F13_VersionZeroIsIncompleteEvenIfTypesExist() => Assert.False(GovernanceRules.IsComplete(0, Required, new[] { "EventPermit", "SafetyPlan" }));

    // ---------- TRUE ----------
    [Fact] public void T01_PdfIsAccepted() => Assert.Equal("application/pdf", GovernanceRules.ValidateFile("permit.pdf", "application/pdf", Pdf, 1024));
    [Fact] public void T02_PngIsAccepted() => Assert.Equal("image/png", GovernanceRules.ValidateFile("plan.png", "image/png", Png, 1024));
    [Fact] public void T03_JpgIsAccepted() => Assert.Equal("image/jpeg", GovernanceRules.ValidateFile("plan.jpg", "image/jpeg", Jpg, 1024));
    [Fact] public void T04_JpegExtensionAndUpperCaseAreAccepted() => Assert.Equal("image/jpeg", GovernanceRules.ValidateFile("PLAN.JPEG", "image/jpeg", Jpg, 1024));
    [Fact] public void T05_FileExactlyAtTheLimitIsAccepted() => Assert.Equal("application/pdf", GovernanceRules.ValidateFile("a.pdf", "application/pdf", Pdf, Pdf.Length));
    [Fact] public void T06_BothRequiredTypesMakeTheSetComplete() => Assert.True(GovernanceRules.IsComplete(1, Required, new[] { "EventPermit", "SafetyPlan" }));
    [Fact] public void T07_ExtraDocumentsDoNotBreakCompleteness() => Assert.True(GovernanceRules.IsComplete(3, Required, new[] { "SafetyPlan", "EventPermit", "Other" }));
    [Fact] public void T08_NoRequiredTypesMeansAnyUploadedVersionIsComplete() => Assert.True(GovernanceRules.IsComplete(1, Array.Empty<string>(), Array.Empty<string>()));
}

public class ReviewComplianceFalseToTrueTests
{
    // ---------- FALSE ----------
    [Theory][InlineData("Approved")][InlineData("Rejected")][InlineData("RequestMoreInfo")]
    public void F01_EmptyNotesAreRefused(string d) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateReview(d, "", true));
    [Theory][InlineData("Approved")][InlineData("Rejected")][InlineData("RequestMoreInfo")]
    public void F02_WhitespaceNotesAreRefused(string d) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateReview(d, "   \t ", true));
    [Fact] public void F03_NullNotesAreRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateReview("Approved", null, true));
    [Fact] public void F04_NotesOver2000AreRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateReview("Approved", new string('x', 2001), true));
    [Theory][InlineData("")][InlineData("approved")][InlineData("Pending")][InlineData("Other")]
    public void F05_UnknownDecisionIsRefused(string d) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateReview(d, "ok", true));
    [Fact] public void F06_RequestMoreInfoIsNotAllowedForScheduleChanges() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateReview("RequestMoreInfo", "ok", false));
    [Fact] public void F07_NotSubmittedRecordCannotPublish() => Assert.False(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus = "NotSubmitted", ComplianceVersion = 1, ComplianceReviewedVersion = null }));
    [Fact] public void F08_PendingRecordCannotPublish() => Assert.False(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus = "PendingReview", ComplianceVersion = 1, ComplianceReviewedVersion = null }));
    [Theory][InlineData("Rejected")][InlineData("RequestMoreInfo")]
    public void F09_NegativeDecisionCannotPublish(string s) => Assert.False(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus = s, ComplianceVersion = 1, ComplianceReviewedVersion = 1 }));
    [Fact] public void F10_ApprovalOfAnOlderVersionCannotPublish() => Assert.False(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus = "Approved", ComplianceVersion = 3, ComplianceReviewedVersion = 2 }));
    [Fact] public void F11_ReplacingAFileClearsTheReviewedVersion() => Assert.False(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus = "Approved", ComplianceVersion = 2, ComplianceReviewedVersion = null }));
    [Fact] public void F12_VersionZeroCannotPublish() => Assert.False(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus = "Approved", ComplianceVersion = 0, ComplianceReviewedVersion = 0 }));

    // ---------- TRUE ----------
    [Theory][InlineData("Approved")][InlineData("Rejected")][InlineData("RequestMoreInfo")]
    public void T01_ComplianceDecisionsWithNotesAreAccepted(string d) => Assert.Equal("Checked", GovernanceRules.ValidateReview(d, "Checked", true));
    [Fact] public void T02_NotesAreTrimmed() => Assert.Equal("Checked", GovernanceRules.ValidateReview("Approved", "  Checked \n", true));
    [Fact] public void T03_NotesAtBothBoundsAreAccepted() { Assert.Equal(1, GovernanceRules.ValidateReview("Approved", "x", true).Length); Assert.Equal(2000, GovernanceRules.ValidateReview("Approved", new string('x', 2000), true).Length); }
    [Theory][InlineData("Approved")][InlineData("Rejected")]
    public void T04_ScheduleChangeDecisionsAreAccepted(string d) => Assert.Equal("ok", GovernanceRules.ValidateReview(d, "ok", false));
    [Fact] public void T05_ApprovedCurrentVersionCanPublish() => Assert.True(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus = "Approved", ComplianceVersion = 1, ComplianceReviewedVersion = 1 }));
    [Fact] public void T06_ApprovedLaterVersionCanPublish() => Assert.True(GovernanceRules.ComplianceApproved(new Event { ComplianceStatus = "Approved", ComplianceVersion = 4, ComplianceReviewedVersion = 4 }));
}

// Report 3 (UC-87 Approve Event Change Request): note is required only when rejecting (10-1000), optional (max 1000) when approving.
public class ReviewChangeRequestNoteFalseToTrueTests
{
    // ---------- FALSE ----------
    [Theory][InlineData("")][InlineData("Maybe")][InlineData("RequestMoreInfo")][InlineData("approved")]
    public void F01_UnknownDecisionIsRefused(string d) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChangeReview(d, "long enough note"));
    [Fact] public void F02_RejectWithoutNoteIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChangeReview("Rejected", null));
    [Fact] public void F03_RejectWithSpacesOnlyIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChangeReview("Rejected", "          "));
    [Fact] public void F04_RejectWithNineCharactersIsRefused() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChangeReview("Rejected", "123456789"));
    [Fact] public void F05_RejectNoteIsTrimmedBeforeCounting() => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChangeReview("Rejected", "   short   "));
    [Theory][InlineData("Approved")][InlineData("Rejected")]
    public void F06_NoteOver1000IsRefused(string d) => Assert.Throws<ArgumentException>(() => GovernanceRules.ValidateChangeReview(d, new string('x', 1001)));

    // ---------- TRUE ----------
    [Fact] public void T01_ApproveWithoutNoteIsAccepted() => Assert.Equal("", GovernanceRules.ValidateChangeReview("Approved", null));
    [Fact] public void T02_ApproveWithSpacesOnlyIsAcceptedAsEmpty() => Assert.Equal("", GovernanceRules.ValidateChangeReview("Approved", "   "));
    [Fact] public void T03_ApproveWithNoteKeepsTrimmedNote() => Assert.Equal("Looks fine", GovernanceRules.ValidateChangeReview("Approved", " Looks fine "));
    [Fact] public void T04_RejectWithTenCharactersIsAccepted() => Assert.Equal("1234567890", GovernanceRules.ValidateChangeReview("Rejected", "1234567890"));
    [Fact] public void T05_RejectWith1000CharactersIsAccepted() => Assert.Equal(1000, GovernanceRules.ValidateChangeReview("Rejected", new string('x', 1000)).Length);
}
