using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TicketAPI.Risk;
using Xunit;

namespace TicketAPI.Tests;

internal sealed class FakeRiskClient : IRiskClient
{
    public RiskResult? IngestResult { get; set; }
    public BlockCheckResult? BlockResult { get; set; } = new();
    public int IngestCalls { get; private set; }
    public int CheckCalls { get; private set; }
    public RiskEvent? LastEvent { get; private set; }

    public Task<RiskResult?> IngestAsync(RiskEvent evt, CancellationToken ct = default)
    { IngestCalls++; LastEvent = evt; return Task.FromResult(IngestResult); }

    public Task<BlockCheckResult?> CheckBlockAsync(int? userId, string? ip, string? deviceFp, string? sessionId, CancellationToken ct = default)
    { CheckCalls++; return Task.FromResult(BlockResult); }
}

public class RiskGuardTests
{
    private readonly FakeRiskClient _client = new();

    private RiskGuard Guard(params (string, string)[] config)
        => new(_client, new SlidingCounter(), TestHttp.Config(config), NullLogger<RiskGuard>.Instance);

    private static RiskEvent WithFraudRule()
    {
        var e = new RiskEvent { UserId = 5 };
        e.FraudRules.Add(new RuleHit("FRD_T01", 1, "8 tickets"));
        return e;
    }

    private static RiskResult Result(string action, bool shadow = false, string? challenge = null)
        => new() { Action = action, IsShadow = shadow, Level = "Critical", Score = 100, ChallengeType = challenge };

    // ------------------------------------------------------------ gửi / không gửi

    [Fact]
    public async Task CleanRequest_DoesNotCallAdminApi()
    {
        var v = await Guard().EvaluateAsync(TestHttp.Browser(), new RiskEvent { UserId = 5 }, isBooking: false);

        Assert.True(v.Allowed);
        Assert.Equal(0, _client.IngestCalls);
    }

    [Fact]
    public async Task FraudRule_IsSentTogetherWithIpDeviceAndSessionFromTheRequest()
    {
        await Guard().EvaluateAsync(TestHttp.Browser(), WithFraudRule(), isBooking: true);

        Assert.Equal(1, _client.IngestCalls);
        Assert.Equal("203.0.113.9", _client.LastEvent!.IpAddress);
        Assert.Equal("fp_test", _client.LastEvent.DeviceFp);
        Assert.Equal("sess-1", _client.LastEvent.SessionId);
        Assert.Equal(5, _client.LastEvent.UserId);
    }

    [Fact]
    public async Task BotRulesFromTheRequest_AreSentEvenWithoutBusinessRules()
    {
        await Guard().EvaluateAsync(TestHttp.Curl(h => h["X-Forwarded-For"] = "203.0.113.20"), new RiskEvent(), isBooking: false);

        Assert.Equal(1, _client.IngestCalls);
        Assert.Contains(_client.LastEvent!.BotRules, r => r.Code == "BOT_H01");
    }

    [Fact]
    public async Task EventWithoutAnyIdentifier_IsNotSent()
    {
        // curl không IP/session/device và không có UserId => AdminAPI sẽ trả 400, nên không gửi
        var v = await Guard().EvaluateAsync(TestHttp.Curl(), new RiskEvent(), isBooking: false);

        Assert.True(v.Allowed);
        Assert.Equal(0, _client.IngestCalls);
    }

    [Fact]
    public async Task WhenRiskIsDisabled_NothingIsCalled()
    {
        var g = Guard(("Risk:Enabled", "false"));

        Assert.True((await g.EvaluateAsync(TestHttp.Curl(), WithFraudRule(), true)).Allowed);
        Assert.True((await g.PreCheckAsync(TestHttp.Browser(), 5)).Allowed);
        Assert.Equal(0, _client.IngestCalls);
        Assert.Equal(0, _client.CheckCalls);
    }

    // ------------------------------------------------------------ thực thi quyết định

    [Fact]
    public async Task ShadowMode_NeverBlocks_EvenWhenEngineSaysBlock()
    {
        _client.IngestResult = Result("BLOCK", shadow: true);
        var v = await Guard().EvaluateAsync(TestHttp.Browser(), WithFraudRule(), true);
        Assert.True(v.Allowed);
    }

    [Fact]
    public async Task Block_Returns403_RiskBlocked()
    {
        _client.IngestResult = Result("BLOCK");
        var v = await Guard().EvaluateAsync(TestHttp.Browser(), WithFraudRule(), true);

        Assert.False(v.Allowed);
        Assert.Equal(403, v.StatusCode);
        Assert.Equal("RISK_BLOCKED", v.Code);
    }

    [Fact]
    public async Task Hold_Returns403_RiskHold()
    {
        _client.IngestResult = Result("HOLD");
        var v = await Guard().EvaluateAsync(TestHttp.Browser(), WithFraudRule(), true);

        Assert.Equal(403, v.StatusCode);
        Assert.Equal("RISK_HOLD", v.Code);
    }

    [Fact]
    public async Task Challenge_Returns428_WithChallengeType()
    {
        _client.IngestResult = Result("CHALLENGE", challenge: "OTP");
        var v = await Guard().EvaluateAsync(TestHttp.Browser(), WithFraudRule(), true);

        Assert.Equal(428, v.StatusCode);
        Assert.Equal("RISK_CHALLENGE", v.Code);
        Assert.Contains("OTP", v.Message);
    }

    [Fact]
    public async Task Allow_PassesThrough()
    {
        _client.IngestResult = Result("ALLOW");
        Assert.True((await Guard().EvaluateAsync(TestHttp.Browser(), WithFraudRule(), true)).Allowed);
    }

    [Fact]
    public async Task AdminApiDown_FailsOpen()
    {
        _client.IngestResult = null;     // RiskClient trả null khi AdminAPI lỗi/quá chậm
        var v = await Guard().EvaluateAsync(TestHttp.Browser(), WithFraudRule(), true);
        Assert.True(v.Allowed);
    }

    // ------------------------------------------------------------ PreCheck

    [Fact]
    public async Task PreCheck_ActiveBlock_Returns403()
    {
        _client.BlockResult = new BlockCheckResult { Blocked = true, Message = "bị khóa" };
        var v = await Guard().PreCheckAsync(TestHttp.Browser(), 5);

        Assert.False(v.Allowed);
        Assert.Equal(403, v.StatusCode);
        Assert.Equal("RISK_BLOCKED", v.Code);
        Assert.Equal("bị khóa", v.Message);
    }

    [Fact]
    public async Task PreCheck_NotBlocked_Passes()
        => Assert.True((await Guard().PreCheckAsync(TestHttp.Browser(), 5)).Allowed);

    [Fact]
    public async Task PreCheck_AdminApiDown_FailsOpen()
    {
        _client.BlockResult = null;
        Assert.True((await Guard().PreCheckAsync(TestHttp.Browser(), 5)).Allowed);
    }

    // ------------------------------------------------------------ định dạng trả về cho frontend

    [Fact]
    public void ToActionResult_UsesTheStatusCodeAndSuccessFalseBody()
    {
        var v = new RiskVerdict(false, 403, "RISK_BLOCKED", "msg", null);
        var r = Assert.IsType<ObjectResult>(v.ToActionResult());

        Assert.Equal(403, r.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(r.Value);
        Assert.Contains("\"success\":false", json);
        Assert.Contains("\"message\":\"msg\"", json);
    }
}
