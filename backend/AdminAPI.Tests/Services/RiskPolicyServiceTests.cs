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

public class RiskPolicyServiceTests : DbTestBase
{
    private const string Reason = "Raise the medium threshold after review";
    private RiskPolicyService Sut() => new(Db, Cfg.Object);

    private static string RuleJson(int score) => "{\"type\":\"Bot\",\"group\":\"bot\",\"window_sec\":60,\"threshold\":5,\"score\":" + score + "}";

    private void Seed(bool withVersion = true)
    {
        Param("risk.mode", "SHADOW");
        Param("risk.threshold.medium", "40");
        Param("risk.threshold.high", "70");
        Param("risk.threshold.critical", "90");
        if (withVersion) Param("risk.rule_version", "v1.3");
        Param("risk.rule.BOT_X", RuleJson(20), "risk_rule");
    }

    private static UpdateRiskPolicyRequest Upd(string reason, params (string Key, string Value)[] items)
        => new() { Reason = reason, Items = items.Select(i => new UpdateRiskPolicyItem { Key = i.Key, Value = i.Value }).ToList() };

    // ===== Function11_RiskPolicyConfig =====

    [Fact] // N
    public async Task UTCID01_Get_NoCategory_ReturnsAllRiskParametersOrdered()
    {
        Seed();
        var r = await Sut().GetAsync(null);
        Assert.Equal(6, r.Count);
        Assert.Equal(r.OrderBy(x => x.Category).ThenBy(x => x.Key).Select(x => x.Key).ToArray(), r.Select(x => x.Key).ToArray());
        Assert.Equal("risk_rule", r[^1].Category);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID02_Get_CategoryRiskConfig_ReturnsOnlyThatCategory()
    {
        Seed();
        var r = await Sut().GetAsync("risk_config");
        Assert.Equal(5, r.Count);
        Assert.All(r, x => Assert.Equal("risk_config", x.Category));
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID03_Get_UnknownCategory_Throws400()
    {
        Seed();
        await Err(() => Sut().GetAsync("other"), 400, "Category must be");
    }

    [Fact] // N
    public async Task UTCID04_Update_ValidThreshold_AppliesChangeAndBumpsRuleVersion()
    {
        Seed();
        var r = await Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "45")), 1);
        var medium = r.Changes.Single(c => c.Key == "risk.threshold.medium");
        Assert.Equal("40", medium.OldValue);
        Assert.Equal("45", medium.NewValue);
        var ver = r.Changes.Single(c => c.Key == "risk.rule_version");
        Assert.Equal("v1.3", ver.OldValue);
        Assert.Equal("v1.4", ver.NewValue);
        var row = Db.SystemParameters.Single(p => p.ParamKey == "risk.threshold.medium");
        Assert.Equal("45", row.ParamValue);
        Assert.Equal(1, row.UpdatedBy);
        Cfg.Verify(c => c.Invalidate(), Times.Once);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID05_Update_RuleScoreWithinRange_IsAccepted()
    {
        Seed();
        var r = await Sut().UpdateAsync(Upd(Reason, ("risk.rule.BOT_X", RuleJson(30))), 1);
        Assert.Contains(r.Changes, c => c.Key == "risk.rule.BOT_X" && c.NewValue == RuleJson(30));
        Assert.Contains(r.Changes, c => c.Key == "risk.rule_version" && c.NewValue == "v1.4");
        Cfg.Verify(c => c.Invalidate(), Times.Once);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID06_Update_TwoItemsOneUnchanged_AppliesOnlyTheChangedOne()
    {
        Seed();
        var r = await Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "40"), ("risk.mode", "ENFORCE")), 1);
        Assert.DoesNotContain(r.Changes, c => c.Key == "risk.threshold.medium");
        Assert.Contains(r.Changes, c => c.Key == "risk.mode" && c.NewValue == "ENFORCE");
        Assert.Equal("ENFORCE", Db.SystemParameters.Single(p => p.ParamKey == "risk.mode").ParamValue);
        Cfg.Verify(c => c.Invalidate(), Times.Once);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID07_Update_ExplicitRuleVersion_IsNotBumpedAgain()
    {
        Seed();
        var r = await Sut().UpdateAsync(Upd(Reason, ("risk.rule_version", "v2.0")), 1);
        Assert.Single(r.Changes);
        Assert.Equal("v2.0", Db.SystemParameters.Single(p => p.ParamKey == "risk.rule_version").ParamValue);
        Cfg.Verify(c => c.Invalidate(), Times.Once);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID08_Update_NoRuleVersionRow_UpdatesWithoutVersionChange()
    {
        Seed(withVersion: false);
        var r = await Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "45")), 1);
        Assert.Single(r.Changes);
        Assert.DoesNotContain(r.Changes, c => c.Key == "risk.rule_version");
        Cfg.Verify(c => c.Invalidate(), Times.Once);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID09_Update_Reason10Characters_IsAccepted()
    {
        Seed();
        var r = await Sut().UpdateAsync(Upd("1234567890", ("risk.threshold.medium", "45")), 1);
        Assert.NotEmpty(r.Changes);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID10_Update_Reason500Characters_IsAccepted()
    {
        Seed();
        var r = await Sut().UpdateAsync(Upd(new string('x', 500), ("risk.threshold.medium", "45")), 1);
        Assert.NotEmpty(r.Changes);
        Assert.Equal(1, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID11_Update_Reason9Characters_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd("123456789", ("risk.threshold.medium", "45")), 1), 400, "A reason is required");
        Assert.Equal(0, Db.SaveCalls);
        Cfg.Verify(c => c.Invalidate(), Times.Never);
    }

    [Fact] // B
    public async Task UTCID12_Update_Reason501Characters_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(new string('x', 501), ("risk.threshold.medium", "45")), 1), 400, "A reason is required");
        Assert.Equal(0, Db.SaveCalls);
        Cfg.Verify(c => c.Invalidate(), Times.Never);
    }

    [Fact] // A
    public async Task UTCID13_Update_BlankReason_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd("     ", ("risk.threshold.medium", "45")), 1), 400, "A reason is required");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID14_Update_EmptyItemList_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason), 1), 400, "At least one parameter");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID15_Update_NullItemList_Throws400()
    {
        Seed();
        var req = new UpdateRiskPolicyRequest { Reason = Reason, Items = null! };
        await Err(() => Sut().UpdateAsync(req, 1), 400, "At least one parameter");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID16_Update_DuplicateKeys_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "45"), ("RISK.THRESHOLD.MEDIUM", "46")), 1), 400, "appears more than once");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID17_Update_UnknownKey_Throws404()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.unknown", "1")), 1), 404, "Parameter risk.unknown does not exist.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID18_Update_EmptyValue_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "")), 1), 400, "value must be 1-500");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID19_Update_Value501Characters_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", new string('9', 501))), 1), 400, "value must be 1-500");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID20_Update_SameValueAsStored_Throws409()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "40")), 1), 409, "No parameter value has changed.");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID21_Update_ModeNotAllowed_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.mode", "LIVE")), 1), 400, "risk.mode must be");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID22_Update_NonNumericThreshold_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "abc")), 1), 400, "Invalid configuration");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID23_Update_MediumThresholdAboveHigh_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "95")), 1), 400, "Thresholds must satisfy");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID24_Update_RuleScoreAbove100_Throws400()
    {
        Seed();
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.rule.BOT_X", RuleJson(120))), 1), 400, "Rule BOT_X: score must be");
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID25_Update_ConcurrentUpdate_Throws409()
    {
        Seed();
        Db.ThrowOnSave = new DbUpdateConcurrencyException("conflict");
        await Err(() => Sut().UpdateAsync(Upd(Reason, ("risk.threshold.medium", "45")), 1), 409, "The data was just updated");
    }

}
