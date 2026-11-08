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

public class FraudAlertServiceTests : DbTestBase
{
    private const int Staff = 99;
    private const string Reason = "Verified the customer by phone";
    private FraudAlertService Sut() => new(Db, Cfg.Object, new RiskScoreCalculator(), Mock.Of<IAiSummaryQueue>(), NullLogger<FraudAlertService>.Instance);
    private static BlockRequest BReq(string scope = "ACCOUNT", int hours = 24, string reason = "Suspicious refund abuse pattern", int? userId = 7, int? ticketId = 100, int? alertId = null, string? ip = null)
        => new() { Scope = scope, DurationHours = hours, Reason = reason, UserId = userId, TicketId = ticketId, FraudAlertId = alertId, IpAddress = ip };

    // ===== Function1_FraudDashboard =====

    [Fact] // N
    public async Task UTCID01_Summary_DefaultRange_ReturnsTotalsAndOrderedLevels()
    {
        Alert(1, "Open", "Critical"); Alert(2, "Open", "High"); Alert(3, "InReview", "Medium"); Alert(4, "Resolved", "Low");
        var r = await Sut().GetSummaryAsync(null, null);
        Assert.Equal(4, r.Total);
        Assert.Equal(new[] { "Critical", "High", "Medium", "Low" }, r.ByLevel.Select(x => x.Key).ToArray());
        Assert.InRange((r.To - r.From).TotalDays, 6.9, 7.1);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID02_Summary_OpenHighAlertOlderThanSla_CountedAsOverdue()
    {
        Alert(1, "Open", "High", created: DateTime.UtcNow.AddHours(-5)); Alert(2, "Open", "Low");
        var r = await Sut().GetSummaryAsync(null, null);
        Assert.Equal(1, r.OpenHighOrCritical);
        Assert.Equal(1, r.OverdueSla);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID03_Summary_OpenHighAlertWithinSla_NotOverdue()
    {
        Alert(1, "Open", "High", created: DateTime.UtcNow.AddMinutes(-30));
        var r = await Sut().GetSummaryAsync(null, null);
        Assert.Equal(1, r.OpenHighOrCritical);
        Assert.Equal(0, r.OverdueSla);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID04_Summary_NoAlertsInRange_ReturnsZeroTotals()
    {
        var r = await Sut().GetSummaryAsync(null, null);
        Assert.Equal(0, r.Total);
        Assert.All(r.ByLevel, x => Assert.Equal(0, x.Count));
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID05_Summary_FromEqualsTo_IsAccepted()
    {
        var t = DateTime.UtcNow.AddDays(-1);
        Alert(1, created: t);
        var r = await Sut().GetSummaryAsync(t, t);
        Assert.NotNull(r);
        Assert.InRange(r.Total, 0, 1);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID06_Summary_FromLaterThanTo_Throws400()
    {
        await Err(() => Sut().GetSummaryAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)), 400, "From must be");
    }

    [Fact] // N
    public async Task UTCID07_Search_NoFilter_ReturnsNewestFirstWithPaging()
    {
        Alert(1, created: DateTime.UtcNow.AddDays(-3)); Alert(2, created: DateTime.UtcNow.AddDays(-2)); Alert(3, created: DateTime.UtcNow.AddDays(-1));
        var r = await Sut().SearchAsync(new FraudAlertQuery { Page = 1, PageSize = 20 });
        Assert.Equal(new[] { 3, 2, 1 }, r.Items.Select(i => i.FraudAlertId).ToArray());
        Assert.Equal(3, r.TotalCount);
        Assert.Equal(1, r.Page);
        Assert.Equal(20, r.PageSize);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID08_Search_FilterByLevelStatusAndUser_ReturnsOnlyMatches()
    {
        Alert(1, "Open", "High", userId: 7); Alert(2, "Open", "Low", userId: 7); Alert(3, "Resolved", "High", userId: 7); Alert(4, "Open", "High", userId: 8);
        var r = await Sut().SearchAsync(new FraudAlertQuery { RiskLevel = "High", Status = "Open", UserId = 7, From = DateTime.UtcNow.AddDays(-3), To = DateTime.UtcNow, Page = 1, PageSize = 20 });
        Assert.Single(r.Items);
        Assert.Equal(1, r.Items[0].FraudAlertId);
        Assert.Equal(1, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID09_Search_NoMatchingAlerts_ReturnsEmptyPage()
    {
        var r = await Sut().SearchAsync(new FraudAlertQuery { Page = 1, PageSize = 20 });
        Assert.Empty(r.Items);
        Assert.Equal(0, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID10_Search_PageSizeZero_ClampedToOne()
    {
        Alert(1); Alert(2);
        var r = await Sut().SearchAsync(new FraudAlertQuery { Page = 1, PageSize = 0 });
        Assert.Equal(1, r.PageSize);
        Assert.Single(r.Items);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID11_Search_PageSizeOver100_ClampedTo100()
    {
        Alert(1);
        var r = await Sut().SearchAsync(new FraudAlertQuery { Page = 1, PageSize = 101 });
        Assert.Equal(100, r.PageSize);
        Assert.Equal(0, Db.SaveCalls);
    }

    // ===== Function2_FraudAlertDetail =====

    [Fact] // N
    public async Task UTCID01_Detail_ExistingAlert_ReturnsAlertAndDecisionsOldestFirst()
    {
        Alert(10, session: "session-abcdefghijkl", ai: "{\"verdict\":\"fraud\"}");
        Decision(21, alertId: 10, created: DateTime.UtcNow.AddHours(-1), session: "session-abcdefghijkl", device: "device-abcdefghijkl", ip: "203.0.113.9");
        Decision(20, alertId: 10, created: DateTime.UtcNow.AddHours(-3), session: "session-abcdefghijkl", device: "device-abcdefghijkl", ip: "203.0.113.9");
        var r = await Sut().GetDetailAsync(10);
        Assert.Equal(10, r.Alert.FraudAlertId);
        Assert.Equal(new[] { 20, 21 }, r.Decisions.Select(d => d.RiskDecisionId).ToArray());
        Assert.NotEqual("session-abcdefghijkl", r.Decisions[0].SessionId);
        Assert.StartsWith("session-", r.Decisions[0].SessionId);
        Assert.NotEqual("device-abcdefghijkl", r.Decisions[0].DeviceFp);
        Assert.NotEqual("203.0.113.9", r.Decisions[0].IpAddress);
        Assert.NotNull(r.AiSummary);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID02_Detail_SessionAndDeviceExactly8Characters_NotMasked()
    {
        Alert(10);
        Decision(20, alertId: 10, session: "abcdefgh", device: "12345678", ip: "203.0.113.9");
        var r = await Sut().GetDetailAsync(10);
        Assert.Equal("abcdefgh", r.Decisions[0].SessionId);
        Assert.Equal("12345678", r.Decisions[0].DeviceFp);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID03_Detail_NullSessionDeviceAndIp_ReturnNull()
    {
        Alert(10);
        Decision(20, alertId: 10, session: null, device: null, ip: null);
        var r = await Sut().GetDetailAsync(10);
        Assert.Null(r.Decisions[0].SessionId);
        Assert.Null(r.Decisions[0].DeviceFp);
        Assert.Null(r.Decisions[0].IpAddress);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID04_Detail_AlertWithoutDecisions_ReturnsEmptyList()
    {
        Alert(10);
        var r = await Sut().GetDetailAsync(10);
        Assert.Equal(10, r.Alert.FraudAlertId);
        Assert.Empty(r.Decisions);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID05_Detail_MalformedAiSummary_ReturnsNullSummary()
    {
        Alert(10, ai: "{not json");
        var r = await Sut().GetDetailAsync(10);
        Assert.Equal(10, r.Alert.FraudAlertId);
        Assert.Null(r.AiSummary);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID06_Detail_AlertNotFound_Throws404()
    {
        await Err(() => Sut().GetDetailAsync(9999), 404, "Alert not found.");
    }

    [Fact] // B
    public async Task UTCID07_Detail_IdZero_Throws404()
    {
        await Err(() => Sut().GetDetailAsync(0), 404, "Alert not found.");
    }

    [Fact] // B
    public async Task UTCID08_Detail_NegativeId_Throws404()
    {
        await Err(() => Sut().GetDetailAsync(-1), 404, "Alert not found.");
    }

    // ===== Function3_BlockAccountTicket =====

    [Fact] // N
    public async Task UTCID01_Block_Account_ValidRequest_CreatesActiveBlock()
    {
        var r = await Sut().BlockAsync(BReq(), Staff);
        var d = Db.RiskDecisions.Single(x => x.RiskDecisionId == r.RiskDecisionId);
        Assert.Equal("Active", r.Status);
        Assert.Equal("BLOCK", d.Action);
        Assert.Equal("STAFF", d.DecidedByType);
        Assert.Equal(Staff, d.DecidedBy);
        Assert.Equal("MANUAL_BLOCK", d.ReasonCode);
        Assert.Equal(1, Db.SaveCalls);
        Assert.Single(Db.RiskNotifications.ToList());
    }

    [Fact] // N
    public async Task UTCID02_Block_Ticket_ValidRequest_CreatesActiveBlock()
    {
        var before = DateTime.UtcNow;
        var r = await Sut().BlockAsync(BReq("TICKET"), Staff);
        var d = Db.RiskDecisions.Single(x => x.RiskDecisionId == r.RiskDecisionId);
        Assert.Equal("TICKET", d.Scope);
        Assert.Equal(100, d.TicketId);
        Assert.InRange(d.ExpiresAt!.Value, before.AddHours(24), DateTime.UtcNow.AddHours(24));
        Assert.Equal(1, Db.SaveCalls);
        Assert.Single(Db.RiskNotifications.ToList());
    }

    [Fact] // N
    public async Task UTCID03_Block_ScopeLowerCaseWithSpaces_IsNormalised()
    {
        var r = await Sut().BlockAsync(BReq(" account "), Staff);
        var d = Db.RiskDecisions.Single(x => x.RiskDecisionId == r.RiskDecisionId);
        Assert.Equal("ACCOUNT", d.Scope);
        Assert.Equal("Active", r.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID04_Block_LinkedAlert_UsesAlertTargetsAndResolvesAlert()
    {
        var alert = Alert(5, "Open", userId: 7, ticketId: 100);
        var r = await Sut().BlockAsync(BReq(userId: null, ticketId: null, alertId: 5), Staff);
        var d = Db.RiskDecisions.Single(x => x.RiskDecisionId == r.RiskDecisionId);
        Assert.Equal(7, d.UserId);
        Assert.Equal(100, d.TicketId);
        Assert.Equal("Resolved", alert.Status);
        Assert.Equal(Staff, alert.ReviewedBy);
        Assert.Equal(1, Db.SaveCalls);
        Assert.Single(Db.RiskNotifications.ToList());
    }

    [Fact] // N
    public async Task UTCID05_Block_LinkedAlertWithPendingAppeal_KeepsAlertStatus()
    {
        var alert = Alert(5, "Open", userId: 7, ticketId: 100);
        Decision(21, "BLOCK", "Overturned", alertId: 5, userId: 7);
        Appeal(60, 21, 7, "Pending");
        var r = await Sut().BlockAsync(BReq(userId: null, ticketId: null, alertId: 5), Staff);
        Assert.Equal("Open", alert.Status);
        Assert.Equal("Active", r.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID06_Block_Duration1Hour_IsAccepted()
    {
        var before = DateTime.UtcNow;
        var r = await Sut().BlockAsync(BReq(hours: 1), Staff);
        Assert.InRange(r.ExpiresAt!.Value, before.AddHours(1), DateTime.UtcNow.AddHours(1));
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID07_Block_Duration720Hours_IsAccepted()
    {
        var before = DateTime.UtcNow;
        var r = await Sut().BlockAsync(BReq(hours: 720), Staff);
        Assert.InRange(r.ExpiresAt!.Value, before.AddHours(720), DateTime.UtcNow.AddHours(720));
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID08_Block_Duration0_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq(hours: 0), Staff), 400, "DurationHours must be");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID09_Block_Duration721_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq(hours: 721), Staff), 400, "DurationHours must be");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID10_Block_ScopeNotAllowed_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq("DEVICE"), Staff), 400, "Manual block scope");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID11_Block_Reason9Characters_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq(reason: "123456789"), Staff), 400, "A reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID12_Block_Reason10Characters_IsAccepted()
    {
        var r = await Sut().BlockAsync(BReq(reason: "1234567890"), Staff);
        Assert.Equal("Active", r.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID13_Block_Reason501Characters_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq(reason: new string('x', 501)), Staff), 400, "A reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID14_Block_AlertNotFound_Throws404()
    {
        await Err(() => Sut().BlockAsync(BReq(alertId: 9999), Staff), 404, "Alert not found.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID15_Block_InvalidIpAddress_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq(ip: "abc"), Staff), 400, "IpAddress is invalid.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID16_Block_TicketWithoutOwnerUserId_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq("TICKET", userId: null), Staff), 400, "Blocking a ticket requires");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID17_Block_TicketWithoutTicketId_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq("TICKET", ticketId: null), Staff), 400, "Scope TICKET requires");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID18_Block_OwnAccount_Throws400()
    {
        await Err(() => Sut().BlockAsync(BReq(userId: Staff), Staff), 400, "You cannot block your own account.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID19_Block_TargetOnWhitelist_Throws400()
    {
        Db.RiskLists.Add(new RiskList { ListType = "WHITELIST", ValueType = "USER", ValueHash = "7", Reason = "VIP", CreatedAt = DateTime.UtcNow });
        Db.SaveChanges();
        await Err(() => Sut().BlockAsync(BReq(), Staff), 400, "internal whitelist");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID20_Block_TargetAlreadyBlocked_Throws409()
    {
        Decision(20, "BLOCK", "Active", userId: 7, scope: "ACCOUNT", expires: DateTime.UtcNow.AddHours(5));
        await Err(() => Sut().BlockAsync(BReq(), Staff), 409, "already blocked");
        Assert.Equal(0, Db.SaveCalls);
    }

    // ===== Function4_RestoreAccountTicket =====

    [Fact] // N
    public async Task UTCID01_Restore_ActiveBlock_SetsRestoredAndNotifiesOwner()
    {
        var alert = Alert(5, "Open", userId: 7); var d = Decision(20, alertId: 5);
        var r = await Sut().RestoreAsync(20, "  Verified the customer by phone  ", Staff);
        Assert.Equal("Restored", d.Status);
        Assert.Equal(Staff, d.RestoredBy);
        Assert.NotNull(d.RestoredAt);
        Assert.Equal("Verified the customer by phone", d.RestoreReason);
        Assert.Equal("FalsePositive", alert.Status);
        Assert.Equal(1, Db.SaveCalls);
        Assert.Single(Db.RiskNotifications.ToList());
    }

    [Fact] // N
    public async Task UTCID02_Restore_ActiveHold_IsRestored()
    {
        Alert(5, "Open", userId: 7); var d = Decision(20, "HOLD", alertId: 5);
        await Sut().RestoreAsync(20, Reason, Staff);
        Assert.Equal("Restored", d.Status);
        Assert.Equal(1, Db.SaveCalls);
        Assert.Single(Db.RiskNotifications.ToList());
    }

    [Fact] // N
    public async Task UTCID03_Restore_AnotherActiveDecisionOnAlert_KeepsAlertStatus()
    {
        var alert = Alert(5, "Open", userId: 7); var d = Decision(20, alertId: 5); Decision(21, alertId: 5, scope: "TICKET", ticketId: 100);
        await Sut().RestoreAsync(20, Reason, Staff);
        Assert.Equal("Restored", d.Status);
        Assert.Equal("Open", alert.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID04_Restore_PendingAppealOnAlert_KeepsAlertStatus()
    {
        var alert = Alert(5, "Open", userId: 7); var d = Decision(20, alertId: 5); Decision(22, "BLOCK", "Overturned", alertId: 5); Appeal(60, 22, 7, "Pending");
        await Sut().RestoreAsync(20, Reason, Staff);
        Assert.Equal("Restored", d.Status);
        Assert.Equal("Open", alert.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID05_Restore_DecisionWithoutOwner_NoNotification()
    {
        Alert(5, "Open", userId: null); var d = Decision(20, alertId: 5, userId: null);
        await Sut().RestoreAsync(20, Reason, Staff);
        Assert.Equal("Restored", d.Status);
        Assert.Empty(Db.RiskNotifications.ToList());
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID06_Restore_Reason10Characters_IsAccepted()
    {
        Alert(5); var d = Decision(20, alertId: 5);
        await Sut().RestoreAsync(20, "1234567890", Staff);
        Assert.Equal("Restored", d.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID07_Restore_Reason500Characters_IsAccepted()
    {
        Alert(5); var d = Decision(20, alertId: 5);
        await Sut().RestoreAsync(20, new string('x', 500), Staff);
        Assert.Equal("Restored", d.Status);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID08_Restore_Reason9Characters_Throws400()
    {
        Alert(5); Decision(20, alertId: 5);
        await Err(() => Sut().RestoreAsync(20, "123456789", Staff), 400, "A reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID09_Restore_Reason501Characters_Throws400()
    {
        Alert(5); Decision(20, alertId: 5);
        await Err(() => Sut().RestoreAsync(20, new string('x', 501), Staff), 400, "A reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID10_Restore_ReasonNull_Throws400()
    {
        Alert(5); Decision(20, alertId: 5);
        await Err(() => Sut().RestoreAsync(20, null!, Staff), 400, "A reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID11_Restore_DecisionNotFound_Throws404()
    {
        await Err(() => Sut().RestoreAsync(9999, Reason, Staff), 404, "Decision not found.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID12_Restore_ActionChallenge_Throws400()
    {
        Decision(20, "CHALLENGE");
        await Err(() => Sut().RestoreAsync(20, Reason, Staff), 400, "Only Block/Hold");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID13_Restore_ShadowDecision_Throws400()
    {
        Decision(20, shadow: true);
        await Err(() => Sut().RestoreAsync(20, Reason, Staff), 400, "A shadow decision");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID14_Restore_AlreadyRestored_Throws409()
    {
        Decision(20, status: "Restored");
        await Err(() => Sut().RestoreAsync(20, Reason, Staff), 409, "The decision is in status Restored");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID15_Restore_Overturned_Throws409()
    {
        Decision(20, status: "Overturned");
        await Err(() => Sut().RestoreAsync(20, Reason, Staff), 409, "The decision is in status Overturned");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID16_Restore_ConcurrentUpdate_Throws409()
    {
        Alert(5); Decision(20, alertId: 5);
        Db.ThrowOnSave = new DbUpdateConcurrencyException("conflict");
        await Err(() => Sut().RestoreAsync(20, Reason, Staff), 409, "The data was just updated");
    }

}
