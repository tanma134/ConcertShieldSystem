using AuthenticationAPI.Risk;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuthenticationAPI.Tests;

/// <summary>BOT_S02 (đăng ký hàng loạt từ một IP) và FRD_A02 (email dùng một lần / ngẫu nhiên).</summary>
public class AuthRiskServiceTests
{
    private readonly Mock<IRiskGuard> _guard = new();
    private readonly List<RiskEvent> _sent = new();
    private readonly AuthRiskService _svc;

    public AuthRiskServiceTests()
    {
        _guard.Setup(g => g.PreCheckAsync(It.IsAny<HttpContext>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(RiskVerdict.Allow());
        _guard.Setup(g => g.EvaluateAsync(It.IsAny<HttpContext>(), It.IsAny<RiskEvent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
              .Callback<HttpContext, RiskEvent, bool, CancellationToken>((_, e, _, _) => _sent.Add(e))
              .ReturnsAsync(RiskVerdict.Allow());

        _svc = new AuthRiskService(_guard.Object, new SlidingCounter(), NullLogger<AuthRiskService>.Instance);
    }

    private static DefaultHttpContext From(string ip)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = ip;
        return ctx;
    }

    private static string[] Fraud(RiskEvent e) => e.FraudRules.Select(r => r.Code).ToArray();
    private static string[] Bot(RiskEvent e) => e.BotRules.Select(r => r.Code).ToArray();

    // ------------------------------------------------------------ FRD_A02

    [Theory]
    [InlineData("someone@mailinator.com")]
    [InlineData("someone@YOPMAIL.com")]
    [InlineData("x8f3k29x7d1q09@gmail.com")]   // phần trước @ nhiều chữ số, trông ngẫu nhiên
    [InlineData("bcdfghjklmnpqr@gmail.com")]   // 14 ký tự không có nguyên âm
    public async Task SuspiciousEmail_RaisesA02(string email)
    {
        await _svc.CheckRegisterAsync(From("203.0.113.1"), email);

        Assert.Contains("FRD_A02", Fraud(_sent.Single()));
    }

    [Theory]
    [InlineData("nguyen.van.a@gmail.com")]
    [InlineData("nguyenvana2024@gmail.com")]
    [InlineData("a@b.co")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("khong-co-dau-a-cong")]
    public async Task NormalEmail_DoesNotRaiseA02(string? email)
    {
        await _svc.CheckRegisterAsync(From("203.0.113.1"), email);

        Assert.DoesNotContain("FRD_A02", Fraud(_sent.Single()));
    }

    // ------------------------------------------------------------ BOT_S02

    [Fact]
    public async Task S02_RaisedOnTheFifthRegistrationFromOneIp()
    {
        var ctx = From("203.0.113.50");

        for (var i = 1; i <= 4; i++) await _svc.CheckRegisterAsync(ctx, $"user{i}@gmail.com");
        Assert.All(_sent, e => Assert.DoesNotContain("BOT_S02", Bot(e)));

        await _svc.CheckRegisterAsync(ctx, "user5@gmail.com");
        Assert.Contains("BOT_S02", Bot(_sent.Last()));
    }

    [Fact]
    public async Task S02_CountsPerIp()
    {
        for (var i = 1; i <= 6; i++) await _svc.CheckRegisterAsync(From("203.0.113.60"), $"u{i}@gmail.com");

        await _svc.CheckRegisterAsync(From("203.0.113.61"), "other@gmail.com");

        Assert.DoesNotContain("BOT_S02", Bot(_sent.Last()));
    }

    [Fact]
    public async Task S02_NotCountedWithoutAUsableIp()
    {
        var ctx = new DefaultHttpContext();     // không IP

        for (var i = 1; i <= 8; i++) await _svc.CheckRegisterAsync(ctx, $"u{i}@gmail.com");

        Assert.All(_sent, e => Assert.DoesNotContain("BOT_S02", Bot(e)));
    }

    // ------------------------------------------------------------ block

    [Fact]
    public async Task Register_ActiveBlock_StopsBeforeEvaluating()
    {
        _guard.Setup(g => g.PreCheckAsync(It.IsAny<HttpContext>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new RiskVerdict(false, 403, "RISK_BLOCKED", "blocked", null));

        var v = await _svc.CheckRegisterAsync(From("203.0.113.1"), "a@gmail.com");

        Assert.False(v.Allowed);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Login_ActiveBlock_Returns403()
    {
        _guard.Setup(g => g.PreCheckAsync(It.IsAny<HttpContext>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new RiskVerdict(false, 403, "RISK_BLOCKED", "blocked", null));

        var v = await _svc.CheckLoginAsync(From("203.0.113.1"));

        Assert.Equal(403, v.StatusCode);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Login_NotBlocked_IsPassedToTheGuardWithoutUser()
    {
        var v = await _svc.CheckLoginAsync(From("203.0.113.1"));

        Assert.True(v.Allowed);
        Assert.Null(_sent.Single().UserId);      // lúc đăng nhập chưa biết userId
    }
}
