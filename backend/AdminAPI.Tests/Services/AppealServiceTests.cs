// Generated from the Unit Test Case matrix (AdminAPI_Unit_Test_Case.xlsx). Method names = TestMapping.
using System.Security.Claims;
using AdminAPI.Controllers;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using AdminAPI.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AdminAPI.Tests.Services;

public class AppealServiceTests : DbTestBase
{
    private const int Staff = 99;
    private AppealService Sut() => new(Db, Cfg.Object);

    private static SubmitAppealRequest Req(int decisionId = 40, string reason = "I did not do anything wrong, please review.", int files = 0,
        string url = "https://cdn.example.com/proof.jpg", string name = "proof", long size = 1024 * 1024)
        => new()
        {
            RiskDecisionId = decisionId, Reason = reason,
            Evidence = Enumerable.Range(0, files).Select(i => new EvidenceFileDto
            { FileName = name == "proof" ? $"proof{i}.jpg" : name, Url = url, ContentType = "image/jpeg", SizeBytes = size }).ToList()
        };

    private static ResolveAppealRequest Res(string decision, string note = "Customer provided valid proof of purchase")
        => new() { Decision = decision, ReviewNote = note };

    private void SeedAppeals()
    {
        Decision(30, userId: 7); Decision(31, userId: 8); Decision(32, userId: 9); Decision(33, userId: 10);
        Appeal(50, 30, 7, "Pending", sla: DateTime.UtcNow.AddHours(-5));
        Appeal(51, 31, 8, "Pending", sla: DateTime.UtcNow.AddHours(10));
        Appeal(52, 32, 9, "Accepted");
        Appeal(53, 33, 10, "Rejected");
    }

    // ===== Function5_RiskDecisionDetail =====

    [Fact] // N
    public async Task UTCID01_GetAdminDecision_Existing_ReturnsFullDetailsWithMaskedIdentifiers()
    {
        Decision(30, userId: 7, alertId: null, session: "session-abcdefghijkl", device: "device-abcdefghijkl", ip: "203.0.113.9");
        var r = await Sut().GetAdminDecisionAsync(30);
        Assert.Equal(30, r.RiskDecisionId);
        Assert.Equal("RD-30", r.DecisionCode);
        Assert.Equal(7, r.UserId);
        Assert.NotEqual("session-abcdefghijkl", r.SessionId);
        Assert.NotEqual("device-abcdefghijkl", r.DeviceFp);
        Assert.NotEqual("203.0.113.9", r.IpAddress);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID02_GetAdminDecision_NotFound_Throws404()
    {
        await AErr(() => Sut().GetAdminDecisionAsync(9999), 404, "Decision not found.");
    }

    [Fact] // N
    public async Task UTCID03_GetPublicDecision_Owner_ReturnsLimitedFieldsAndCanAppeal()
    {
        Decision(30, userId: 7, notified: DateTime.UtcNow.AddDays(-1));
        var r = await Sut().GetPublicDecisionAsync(30, 7);
        Assert.Equal("RD-30", r.DecisionCode);
        Assert.Equal("BLOCK", r.Action);
        Assert.Equal("Active", r.Status);
        Assert.True(r.CanAppeal);
        Assert.Null(r.CannotAppealReason);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID04_GetPublicDecision_ReasonPublicNull_UsesDefaultText()
    {
        Decision(30, userId: 7, reasonPublic: null);
        var r = await Sut().GetPublicDecisionAsync(30, 7);
        Assert.False(string.IsNullOrWhiteSpace(r.ReasonPublic));
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID05_GetPublicDecision_ExistingAppeal_ReturnsAppealInfoAndCannotAppeal()
    {
        Decision(30, userId: 7); Appeal(50, 30, 7, "Pending");
        var r = await Sut().GetPublicDecisionAsync(30, 7);
        Assert.False(r.CanAppeal);
        Assert.Equal(50, r.ExistingAppealId);
        Assert.Equal("Pending", r.ExistingAppealStatus);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID06_GetPublicDecision_Restored_CannotAppeal()
    {
        Decision(30, userId: 7, status: "Restored");
        var r = await Sut().GetPublicDecisionAsync(30, 7);
        Assert.False(r.CanAppeal);
        Assert.NotNull(r.CannotAppealReason);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID07_GetPublicDecision_BlockExpired_CannotAppeal()
    {
        Decision(30, userId: 7, expires: DateTime.UtcNow.AddHours(-1));
        var r = await Sut().GetPublicDecisionAsync(30, 7);
        Assert.False(r.CanAppeal);
        Assert.NotNull(r.CannotAppealReason);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID08_GetPublicDecision_DeadlinePassed_CannotAppeal()
    {
        Decision(30, userId: 7, expires: DateTime.UtcNow.AddDays(3), notified: DateTime.UtcNow.AddDays(-8));
        var r = await Sut().GetPublicDecisionAsync(30, 7);
        Assert.False(r.CanAppeal);
        Assert.NotNull(r.CannotAppealReason);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID09_GetPublicDecision_OtherUsersDecision_Throws404()
    {
        Decision(30, userId: 8);
        await AErr(() => Sut().GetPublicDecisionAsync(30, 7), 404, "Decision not found.");
    }

    [Fact] // A
    public async Task UTCID10_GetPublicDecision_ShadowDecision_Throws404()
    {
        Decision(30, userId: 7, shadow: true);
        await AErr(() => Sut().GetPublicDecisionAsync(30, 7), 404, "Decision not found.");
    }

    [Fact] // A
    public async Task UTCID11_GetPublicDecision_NotFound_Throws404()
    {
        await AErr(() => Sut().GetPublicDecisionAsync(9999, 7), 404, "Decision not found.");
    }

    // ===== Function6_SubmitAppeal =====

    [Fact] // N
    public async Task UTCID01_Submit_ActiveBlock_CreatesPendingAppeal()
    {
        Decision(40, userId: 7);
        var before = DateTime.UtcNow;
        var r = await Sut().SubmitAppealAsync(Req(reason: "   I did not do anything wrong, please review.   "), 7);
        var a = Db.RiskAppeals.Single();
        Assert.Equal("Pending", r.Status);
        Assert.Equal(7, a.UserId);
        Assert.Equal("I did not do anything wrong, please review.", a.Reason);
        Assert.Null(a.Evidence);
        Assert.InRange(r.SlaDueAt, before.AddHours(48), DateTime.UtcNow.AddHours(48));
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID02_Submit_ActiveHold_CreatesPendingAppeal()
    {
        Decision(40, "HOLD", userId: 7);
        var r = await Sut().SubmitAppealAsync(Req(), 7);
        Assert.Equal("Pending", r.Status);
        Assert.Single(Db.RiskAppeals.ToList());
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID03_Submit_WithEvidenceFile_StoresEvidenceJson()
    {
        Decision(40, userId: 7);
        var r = await Sut().SubmitAppealAsync(Req(files: 1), 7);
        var a = Db.RiskAppeals.Single();
        Assert.Equal("Pending", r.Status);
        Assert.NotNull(a.Evidence);
        Assert.Contains("proof0.jpg", a.Evidence);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID04_Submit_Reason10Characters_IsAccepted()
    {
        Decision(40, userId: 7);
        var r = await Sut().SubmitAppealAsync(Req(reason: "1234567890"), 7);
        Assert.Equal("Pending", r.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID05_Submit_Reason2000Characters_IsAccepted()
    {
        Decision(40, userId: 7);
        var r = await Sut().SubmitAppealAsync(Req(reason: new string('x', 2000)), 7);
        Assert.Equal("Pending", r.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID06_Submit_Reason9Characters_Throws400()
    {
        Decision(40, userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(reason: "123456789"), 7), 400, "An appeal reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID07_Submit_Reason2001Characters_Throws400()
    {
        Decision(40, userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(reason: new string('x', 2001)), 7), 400, "An appeal reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID08_Submit_BlankReason_Throws400()
    {
        Decision(40, userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(reason: "     "), 7), 400, "An appeal reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID09_Submit_FiveEvidenceFiles_AreAccepted()
    {
        Decision(40, userId: 7);
        var r = await Sut().SubmitAppealAsync(Req(files: 5), 7);
        Assert.Equal("Pending", r.Status);
        Assert.NotNull(Db.RiskAppeals.Single().Evidence);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID10_Submit_SixEvidenceFiles_Throws400()
    {
        Decision(40, userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(files: 6), 7), 400, "At most 5 files");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID11_Submit_EvidenceUrlNotAbsolute_Throws400()
    {
        Decision(40, userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(files: 1, url: "/uploads/proof.jpg"), 7), 400, "Each evidence file needs");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID12_Submit_EvidenceEmptyFileName_Throws400()
    {
        Decision(40, userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(files: 1, name: ""), 7), 400, "The evidence file information");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID13_Submit_EvidenceExactly5MB_IsAccepted()
    {
        Decision(40, userId: 7);
        var r = await Sut().SubmitAppealAsync(Req(files: 1, size: 5L * 1024 * 1024), 7);
        Assert.Equal("Pending", r.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID14_Submit_EvidenceOver5MB_Throws400()
    {
        Decision(40, userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(files: 1, size: 5L * 1024 * 1024 + 1), 7), 400, "exceeds");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID15_Submit_EvidenceNegativeSize_Throws400()
    {
        Decision(40, userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(files: 1, size: -1), 7), 400, "The evidence file information");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID16_Submit_DecisionNotFound_Throws404()
    {
        await AErr(() => Sut().SubmitAppealAsync(Req(decisionId: 9999), 7), 404, "Decision not found.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID17_Submit_DecisionOfAnotherUser_Throws404()
    {
        Decision(40, userId: 8);
        await AErr(() => Sut().SubmitAppealAsync(Req(), 7), 404, "Decision not found.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID18_Submit_DecisionAlreadyAppealed_Throws409()
    {
        Decision(40, userId: 7); Appeal(50, 40, 7, "Pending");
        await AErr(() => Sut().SubmitAppealAsync(Req(), 7), 409, "This decision already has an appeal (status");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID19_Submit_ChallengeDecision_Throws409()
    {
        Decision(40, "CHALLENGE", userId: 7);
        await AErr(() => Sut().SubmitAppealAsync(Req(), 7), 409, "Only Block or Hold");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID20_Submit_ShadowDecision_Throws409()
    {
        Decision(40, userId: 7, shadow: true);
        await AErr(() => Sut().SubmitAppealAsync(Req(), 7), 409, "This decision has not been enforced");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID21_Submit_RestoredDecision_Throws409()
    {
        Decision(40, userId: 7, status: "Restored");
        await AErr(() => Sut().SubmitAppealAsync(Req(), 7), 409, "The decision is in status Restored");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID22_Submit_BlockExpired_Throws409()
    {
        Decision(40, userId: 7, expires: DateTime.UtcNow.AddHours(-1));
        await AErr(() => Sut().SubmitAppealAsync(Req(), 7), 409, "This block has expired");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID23_Submit_DeadlinePassed_Throws409()
    {
        Decision(40, userId: 7, expires: DateTime.UtcNow.AddDays(3), notified: DateTime.UtcNow.AddDays(-8));
        await AErr(() => Sut().SubmitAppealAsync(Req(), 7), 409, "7-day deadline");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID24_Submit_ConcurrentDuplicateAppeal_Throws409()
    {
        Decision(40, userId: 7);
        Db.ThrowOnSave = new DbUpdateException("duplicate");
        Db.BeforeThrow = () => { using var other = NewContext(); other.RiskAppeals.Add(new RiskAppeal { RiskAppealId = 77, RiskDecisionId = 40, UserId = 7, Reason = "first", Status = "Pending", SlaDueAt = DateTime.UtcNow.AddHours(24), CreatedAt = DateTime.UtcNow }); other.SaveChanges(); };
        await AErr(() => Sut().SubmitAppealAsync(Req(), 7), 409, "This decision already has an appeal.");
    }

    // ===== Function7_ResolveAppeal =====

    [Fact] // N
    public async Task UTCID01_Resolve_Accept_OverturnsDecisionAndNotifiesCustomer()
    {
        var d = Decision(30, userId: 7); var a = Appeal(50, 30, 7);
        var r = await Sut().ResolveAppealAsync(50, Res("Accept"), Staff);
        Assert.Equal("Accepted", a.Status);
        Assert.Equal(Staff, a.ReviewedBy);
        Assert.NotNull(a.ReviewedAt);
        Assert.Equal("Overturned", d.Status);
        Assert.Contains("Appeal #50 accepted", d.RestoreReason);
        Assert.Contains(30, r.LiftedDecisionIds!);
        Assert.Equal(1, Db.SaveCalls);
        Assert.Single(Db.RiskNotifications.ToList());
    }

    [Fact] // N
    public async Task UTCID02_Resolve_Accept_AlsoOverturnsRelatedActiveDecisions()
    {
        var t = DateTime.UtcNow.AddDays(-1);
        var d1 = Decision(30, userId: 7, created: t); var d2 = Decision(31, userId: 7, created: t, scope: "TICKET", ticketId: 100); Appeal(50, 30, 7);
        var r = await Sut().ResolveAppealAsync(50, Res("Accept"), Staff);
        Assert.Equal("Overturned", d1.Status);
        Assert.Equal("Overturned", d2.Status);
        Assert.Equal(new[] { 30, 31 }, r.LiftedDecisionIds!.OrderBy(x => x).ToArray());
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID03_Resolve_Accept_LastActiveDecisionOfAlert_AlertFalsePositive()
    {
        var alert = Alert(5, "Open"); var d = Decision(30, userId: 7, alertId: 5); Appeal(50, 30, 7);
        await Sut().ResolveAppealAsync(50, Res("Accept"), Staff);
        Assert.Equal("Overturned", d.Status);
        Assert.Equal("FalsePositive", alert.Status);
        Assert.Equal(Staff, alert.ReviewedBy);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID04_Resolve_Accept_AnotherDecisionStillActive_KeepsAlertStatus()
    {
        var alert = Alert(5, "Open"); Decision(30, userId: 7, alertId: 5, created: DateTime.UtcNow.AddDays(-2)); Decision(31, userId: 7, alertId: 5, scope: "TICKET", ticketId: 100, created: DateTime.UtcNow.AddHours(-3)); Appeal(50, 30, 7);
        await Sut().ResolveAppealAsync(50, Res("Accept"), Staff);
        Assert.Equal("Open", alert.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID05_Resolve_Reject_KeepsDecisionActive()
    {
        var d = Decision(30, userId: 7); var a = Appeal(50, 30, 7);
        var r = await Sut().ResolveAppealAsync(50, Res("Reject"), Staff);
        Assert.Equal("Rejected", a.Status);
        Assert.Equal("Active", d.Status);
        Assert.Empty(r.LiftedDecisionIds!);
        Assert.Equal(1, Db.SaveCalls);
        Assert.Single(Db.RiskNotifications.ToList());
    }

    [Fact] // B
    public async Task UTCID06_Resolve_ReviewNote10Characters_IsAccepted()
    {
        Decision(30, userId: 7); var a = Appeal(50, 30, 7);
        await Sut().ResolveAppealAsync(50, Res("Accept", "1234567890"), Staff);
        Assert.Equal("Accepted", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID07_Resolve_ReviewNote1000Characters_IsAccepted()
    {
        Decision(30, userId: 7); var a = Appeal(50, 30, 7);
        await Sut().ResolveAppealAsync(50, Res("Accept", new string('x', 1000)), Staff);
        Assert.Equal("Accepted", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID08_Resolve_ReviewNote9Characters_Throws400()
    {
        Decision(30, userId: 7); Appeal(50, 30, 7);
        await AErr(() => Sut().ResolveAppealAsync(50, Res("Accept", "123456789"), Staff), 400, "A review reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID09_Resolve_ReviewNote1001Characters_Throws400()
    {
        Decision(30, userId: 7); Appeal(50, 30, 7);
        await AErr(() => Sut().ResolveAppealAsync(50, Res("Accept", new string('x', 1001)), Staff), 400, "A review reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID10_Resolve_BlankReviewNote_Throws400()
    {
        Decision(30, userId: 7); Appeal(50, 30, 7);
        await AErr(() => Sut().ResolveAppealAsync(50, Res("Accept", "     "), Staff), 400, "A review reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID11_Resolve_DecisionWordLowerCase_Throws400()
    {
        Decision(30, userId: 7); Appeal(50, 30, 7);
        await AErr(() => Sut().ResolveAppealAsync(50, Res("accept"), Staff), 400, "Decision must be 'Accept'");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID12_Resolve_DecisionWordWithSpaces_IsTrimmed()
    {
        Decision(30, userId: 7); var a = Appeal(50, 30, 7);
        await Sut().ResolveAppealAsync(50, Res(" Accept "), Staff);
        Assert.Equal("Accepted", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID13_Resolve_AppealNotFound_Throws404()
    {
        await AErr(() => Sut().ResolveAppealAsync(9999, Res("Accept"), Staff), 404, "Appeal not found.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID14_Resolve_AlreadyResolved_Throws409()
    {
        Decision(30, userId: 7); Appeal(50, 30, 7, "Accepted");
        await AErr(() => Sut().ResolveAppealAsync(50, Res("Accept"), Staff), 409, "This appeal has already been resolved");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID15_Resolve_ReviewerMadeTheDecision_Throws400()
    {
        Decision(30, userId: 7, by: "STAFF", decidedBy: Staff); Appeal(50, 30, 7);
        await AErr(() => Sut().ResolveAppealAsync(50, Res("Accept"), Staff), 400, "You cannot review an appeal");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID16_Resolve_DecisionMadeByAnotherStaff_IsAllowed()
    {
        var d = Decision(30, userId: 7, by: "STAFF", decidedBy: 12); var a = Appeal(50, 30, 7);
        await Sut().ResolveAppealAsync(50, Res("Accept"), Staff);
        Assert.Equal("Accepted", a.Status);
        Assert.Equal("Overturned", d.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID17_Resolve_ConcurrentUpdate_Throws409()
    {
        Decision(30, userId: 7); Appeal(50, 30, 7);
        Db.ThrowOnSave = new DbUpdateConcurrencyException("conflict");
        await AErr(() => Sut().ResolveAppealAsync(50, Res("Accept"), Staff), 409, "This appeal was just resolved");
    }

    [Fact] // N
    public async Task UTCID18_Search_DefaultPaging_PendingFirstOrderedBySla()
    {
        SeedAppeals();
        var r = await Sut().SearchAppealsAsync(new AppealQuery { Page = 1, PageSize = 20 });
        Assert.Equal(4, r.TotalCount);
        Assert.Equal("Pending", r.Items[0].Status);
        Assert.Equal("Pending", r.Items[1].Status);
        Assert.True(r.Items[0].SlaDueAt <= r.Items[1].SlaDueAt);
        Assert.True(r.Items[0].IsOverdue);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID19_Search_OverdueOnly_ReturnsOnlyOverduePending()
    {
        SeedAppeals();
        var r = await Sut().SearchAppealsAsync(new AppealQuery { Status = "Pending", OverdueOnly = true, Page = 1, PageSize = 20 });
        Assert.Single(r.Items);
        Assert.True(r.Items[0].IsOverdue);
        Assert.Equal("Pending", r.Items[0].Status);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID20_Search_PageSize101_ClampedTo100()
    {
        SeedAppeals();
        var r = await Sut().SearchAppealsAsync(new AppealQuery { Page = 1, PageSize = 101 });
        Assert.Equal(100, r.PageSize);
        Assert.Equal(0, Db.SaveCalls);
    }

}
