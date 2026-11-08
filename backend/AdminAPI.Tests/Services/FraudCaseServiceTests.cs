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

public class FraudCaseServiceTests : DbTestBase
{
    private const int Staff = 99;
    private const string Note = "Confirmed after reviewing the order history";
    private FraudCaseService Sut() => new(Db, Cfg.Object);

    // ===== Function8_FraudCaseList =====

    [Fact] // N
    public async Task UTCID01_Search_NoFilter_ReturnsNewestFirstWithPaging()
    {
        Alert(1, created: DateTime.UtcNow.AddDays(-3)); Alert(2, created: DateTime.UtcNow.AddDays(-2)); Alert(3, created: DateTime.UtcNow.AddDays(-1));
        var r = await Sut().SearchAsync(new FraudCaseQuery { Page = 1, PageSize = 20 });
        Assert.Equal(new[] { 3, 2, 1 }, r.Items.Select(i => i.CaseId).ToArray());
        Assert.Equal(3, r.TotalCount);
        Assert.Equal(1, r.Page);
        Assert.Equal(20, r.PageSize);
        Assert.Equal(r.Items[0].CreatedAt.AddHours(2), r.Items[0].SlaDueAt);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID02_Search_FilterByStatusLevelTypeAssigneeAndUser()
    {
        Alert(1, "Open", "High", "Bot", 7, assignedTo: 5); Alert(2, "Open", "High", "Bot", 7, assignedTo: 6); Alert(3, "Open", "Low", "Bot", 7, assignedTo: 5);
        Alert(4, "Resolved", "High", "Bot", 7, assignedTo: 5); Alert(5, "Open", "High", "Fraud", 7, assignedTo: 5);
        var r = await Sut().SearchAsync(new FraudCaseQuery { Status = "Open", RiskLevel = "High", AlertType = "Bot", AssignedTo = 5, UserId = 7 });
        Assert.Single(r.Items);
        Assert.Equal(1, r.Items[0].CaseId);
        Assert.Equal(1, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID03_Search_UnassignedOnly_ReturnsOpenCasesWithoutAnalyst()
    {
        Alert(1, "Open"); Alert(2, "InReview", assignedTo: 5); Alert(3, "Resolved");
        var r = await Sut().SearchAsync(new FraudCaseQuery { Unassigned = true });
        Assert.Single(r.Items);
        Assert.Equal(1, r.Items[0].CaseId);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID04_Search_OverdueOnly_ReturnsOverdueHighCases()
    {
        Alert(1, "Open", "High", created: DateTime.UtcNow.AddHours(-5)); Alert(2, "Open", "High", created: DateTime.UtcNow.AddMinutes(-30));
        Alert(3, "Open", "Low", created: DateTime.UtcNow.AddHours(-5)); Alert(4, "Resolved", "High", created: DateTime.UtcNow.AddHours(-5));
        var r = await Sut().SearchAsync(new FraudCaseQuery { OverdueOnly = true });
        Assert.Single(r.Items);
        Assert.Equal(1, r.Items[0].CaseId);
        Assert.True(r.Items[0].IsOverdue);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID05_Search_OpenHighCaseWithinSla_IsNotOverdue()
    {
        Alert(1, "Open", "High", created: DateTime.UtcNow.AddMinutes(-30));
        var r = await Sut().SearchAsync(new FraudCaseQuery());
        Assert.Single(r.Items);
        Assert.False(r.Items[0].IsOverdue);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID06_Search_NumericKeyword_MatchesCaseUserOrderOrTicket()
    {
        Alert(12, userId: 7); Alert(13, userId: 12); Alert(14, userId: 8);
        var r = await Sut().SearchAsync(new FraudCaseQuery { Keyword = "12" });
        Assert.Equal(new[] { 12, 13 }, r.Items.Select(i => i.CaseId).OrderBy(x => x).ToArray());
        Assert.Equal(2, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID07_Search_NonNumericKeyword_Throws400()
    {
        await Err(() => Sut().SearchAsync(new FraudCaseQuery { Keyword = "abc" }), 400, "Keyword must be");
    }

    [Fact] // N
    public async Task UTCID08_Search_SortByRiskScoreDescending()
    {
        Alert(1, score: 10m); Alert(2, score: 90m); Alert(3, score: 50m);
        var r = await Sut().SearchAsync(new FraudCaseQuery { SortBy = "riskScore", Desc = true });
        Assert.Equal(new[] { 2, 3, 1 }, r.Items.Select(i => i.CaseId).ToArray());
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID09_Search_SortByCreatedAtAscending()
    {
        Alert(1, created: DateTime.UtcNow.AddDays(-3)); Alert(2, created: DateTime.UtcNow.AddDays(-2)); Alert(3, created: DateTime.UtcNow.AddDays(-1));
        var r = await Sut().SearchAsync(new FraudCaseQuery { Desc = false });
        Assert.Equal(new[] { 1, 2, 3 }, r.Items.Select(i => i.CaseId).ToArray());
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID10_Search_DateRange_ReturnsCasesInsideRange()
    {
        Alert(1, created: DateTime.UtcNow.AddDays(-10)); Alert(2, created: DateTime.UtcNow.AddDays(-2)); Alert(3, created: DateTime.UtcNow.AddDays(-1));
        var r = await Sut().SearchAsync(new FraudCaseQuery { From = DateTime.UtcNow.AddDays(-5), To = DateTime.UtcNow });
        Assert.Equal(new[] { 2, 3 }, r.Items.Select(i => i.CaseId).OrderBy(x => x).ToArray());
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID11_Search_FromLaterThanTo_Throws400()
    {
        await Err(() => Sut().SearchAsync(new FraudCaseQuery { From = DateTime.UtcNow, To = DateTime.UtcNow.AddDays(-1) }), 400, "From must be");
    }

    [Fact] // B
    public async Task UTCID12_Search_NoCases_ReturnsEmptyPage()
    {
        var r = await Sut().SearchAsync(new FraudCaseQuery());
        Assert.Empty(r.Items);
        Assert.Equal(0, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID13_Search_PageSizeZero_ClampedToOne()
    {
        Alert(1); Alert(2);
        var r = await Sut().SearchAsync(new FraudCaseQuery { PageSize = 0 });
        Assert.Equal(1, r.PageSize);
        Assert.Single(r.Items);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID14_Search_PageSizeOver100_ClampedTo100()
    {
        Alert(1);
        var r = await Sut().SearchAsync(new FraudCaseQuery { PageSize = 101 });
        Assert.Equal(100, r.PageSize);
        Assert.Equal(0, Db.SaveCalls);
    }

    // ===== Function9_AssignFraudCase =====

    [Fact] // N
    public async Task UTCID01_Assign_OpenCase_SetsInReviewAndAssignee()
    {
        var a = Alert(10, "Open");
        var r = await Sut().AssignAsync(10, 5, Staff);
        Assert.Equal("InReview", a.Status);
        Assert.Equal(5, a.AssignedTo);
        Assert.NotNull(a.AssignedAt);
        Assert.Equal("Open", r.PreviousStatus);
        Assert.Null(r.PreviousAssignee);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID02_Assign_InReviewCase_ReassignsToAnotherAnalyst()
    {
        var a = Alert(10, "InReview", assignedTo: 6);
        var r = await Sut().AssignAsync(10, 5, Staff);
        Assert.Equal("InReview", a.Status);
        Assert.Equal(5, a.AssignedTo);
        Assert.Equal(6, r.PreviousAssignee);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID03_Assign_AnalystIdOne_IsAccepted()
    {
        var a = Alert(10, "Open");
        await Sut().AssignAsync(10, 1, Staff);
        Assert.Equal(1, a.AssignedTo);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID04_Assign_AnalystIdZero_Throws400()
    {
        Alert(10, "Open");
        await Err(() => Sut().AssignAsync(10, 0, Staff), 400, "AnalystId is invalid.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID05_Assign_NegativeAnalystId_Throws400()
    {
        Alert(10, "Open");
        await Err(() => Sut().AssignAsync(10, -1, Staff), 400, "AnalystId is invalid.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID06_Assign_CaseNotFound_Throws404()
    {
        await Err(() => Sut().AssignAsync(9999, 5, Staff), 404, "Fraud case not found.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID07_Assign_ResolvedCase_Throws409()
    {
        Alert(10, "Resolved");
        await Err(() => Sut().AssignAsync(10, 5, Staff), 409, "The case is in status Resolved");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID08_Assign_DismissedCase_Throws409()
    {
        Alert(10, "Dismissed");
        await Err(() => Sut().AssignAsync(10, 5, Staff), 409, "The case is in status Dismissed");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID09_Assign_AlreadyAssignedToSameAnalyst_Throws409()
    {
        Alert(10, "InReview", assignedTo: 5);
        await Err(() => Sut().AssignAsync(10, 5, Staff), 409, "The case is already assigned");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID10_Assign_ConcurrentUpdate_Throws409()
    {
        Alert(10, "Open");
        Db.ThrowOnSave = new DbUpdateConcurrencyException("conflict");
        await Err(() => Sut().AssignAsync(10, 5, Staff), 409, "The data was just updated");
    }

    // ===== Function10_ResolveFraudCase =====

    [Fact] // N
    public async Task UTCID01_Resolve_Confirmed_SetsResolved()
    {
        var a = Alert(10, "InReview", assignedTo: 5);
        var r = await Sut().ResolveAsync(10, "Confirmed", "  Confirmed after reviewing the order history  ", 5);
        Assert.Equal("Resolved", a.Status);
        Assert.Equal(5, a.ReviewedBy);
        Assert.NotNull(a.ReviewedAt);
        Assert.Contains("Resolved by #5: Confirmed after reviewing the order history", a.Details);
        Assert.Equal("InReview", r.PreviousStatus);
        Assert.Equal(5, r.PreviousAssignee);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID02_Resolve_FalsePositive_SetsFalsePositive()
    {
        var a = Alert(10, "InReview", assignedTo: 5);
        await Sut().ResolveAsync(10, "FalsePositive", Note, 5);
        Assert.Equal("FalsePositive", a.Status);
        Assert.Equal(5, a.ReviewedBy);
        Assert.NotNull(a.ReviewedAt);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID03_Resolve_Dismissed_SetsDismissed()
    {
        var a = Alert(10, "InReview", assignedTo: 5);
        await Sut().ResolveAsync(10, "Dismissed", Note, 5);
        Assert.Equal("Dismissed", a.Status);
        Assert.NotNull(a.ReviewedAt);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID04_Resolve_OutcomeLowerCaseWithSpaces_IsNormalised()
    {
        var a = Alert(10, "InReview", assignedTo: 5);
        await Sut().ResolveAsync(10, " falsepositive ", Note, 5);
        Assert.Equal("FalsePositive", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID05_Resolve_ExistingDetails_NoteIsAppended()
    {
        var a = Alert(10, "InReview", assignedTo: 5, details: "Earlier note");
        await Sut().ResolveAsync(10, "Confirmed", Note, 5);
        Assert.StartsWith("Earlier note" + "\n[", a.Details);
        Assert.Contains(Note, a.Details);
        Assert.Equal("Resolved", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID06_Resolve_ConfirmedWithActiveBlock_IsAllowed()
    {
        var a = Alert(10, "InReview", assignedTo: 5); Decision(20, alertId: 10);
        await Sut().ResolveAsync(10, "Confirmed", Note, 5);
        Assert.Equal("Resolved", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID07_Resolve_FalsePositiveWithExpiredBlock_IsAllowed()
    {
        var a = Alert(10, "InReview", assignedTo: 5); Decision(20, alertId: 10, expires: DateTime.UtcNow.AddHours(-1));
        await Sut().ResolveAsync(10, "FalsePositive", Note, 5);
        Assert.Equal("FalsePositive", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID08_Resolve_Note10Characters_IsAccepted()
    {
        var a = Alert(10, "InReview", assignedTo: 5);
        await Sut().ResolveAsync(10, "Confirmed", "1234567890", 5);
        Assert.Equal("Resolved", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID09_Resolve_Note500Characters_IsAccepted()
    {
        var a = Alert(10, "InReview", assignedTo: 5);
        await Sut().ResolveAsync(10, "Confirmed", new string('x', 500), 5);
        Assert.Equal("Resolved", a.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID10_Resolve_Note9Characters_Throws400()
    {
        Alert(10, "InReview", assignedTo: 5);
        await Err(() => Sut().ResolveAsync(10, "Confirmed", "123456789", 5), 400, "A resolution note is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID11_Resolve_Note501Characters_Throws400()
    {
        Alert(10, "InReview", assignedTo: 5);
        await Err(() => Sut().ResolveAsync(10, "Confirmed", new string('x', 501), 5), 400, "A resolution note is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID12_Resolve_BlankNote_Throws400()
    {
        Alert(10, "InReview", assignedTo: 5);
        await Err(() => Sut().ResolveAsync(10, "Confirmed", "     ", 5), 400, "A resolution note is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID13_Resolve_OutcomeNotAllowed_Throws400()
    {
        Alert(10, "InReview", assignedTo: 5);
        await Err(() => Sut().ResolveAsync(10, "Rejected", Note, 5), 400, "Outcome must be");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID14_Resolve_CaseNotFound_Throws404()
    {
        await Err(() => Sut().ResolveAsync(9999, "Confirmed", Note, 5), 404, "Fraud case not found.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID15_Resolve_CaseAlreadyResolved_Throws409()
    {
        Alert(10, "Resolved", assignedTo: 5);
        await Err(() => Sut().ResolveAsync(10, "Confirmed", Note, 5), 409, "The case is already Resolved.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID16_Resolve_CaseNotAssigned_Throws409()
    {
        Alert(10, "Open", assignedTo: null);
        await Err(() => Sut().ResolveAsync(10, "Confirmed", Note, 5), 409, "The case must be assigned");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID17_Resolve_ActorIsNotTheAssignedAnalyst_Throws403()
    {
        Alert(10, "InReview", assignedTo: 6);
        await Err(() => Sut().ResolveAsync(10, "Confirmed", Note, 5), 403, "Only the assigned analyst");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID18_Resolve_PendingAppeal_Throws409()
    {
        Alert(10, "InReview", assignedTo: 5); Decision(22, "BLOCK", "Overturned", alertId: 10); Appeal(60, 22, 7, "Pending");
        await Err(() => Sut().ResolveAsync(10, "Confirmed", Note, 5), 409, "The case has a pending appeal");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID19_Resolve_FalsePositiveWhileBlockStillActive_Throws409()
    {
        Alert(10, "InReview", assignedTo: 5); Decision(20, alertId: 10);
        await Err(() => Sut().ResolveAsync(10, "FalsePositive", Note, 5), 409, "Cannot close as FalsePositive");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID20_Resolve_ConcurrentUpdate_Throws409()
    {
        Alert(10, "InReview", assignedTo: 5);
        Db.ThrowOnSave = new DbUpdateConcurrencyException("conflict");
        await Err(() => Sut().ResolveAsync(10, "Confirmed", Note, 5), 409, "The data was just updated");
    }

}
