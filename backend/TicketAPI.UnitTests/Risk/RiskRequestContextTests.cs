using System.Net;
using Microsoft.AspNetCore.Http;
using TicketAPI.Risk;
using Xunit;

namespace TicketAPI.Tests;

public class RiskRequestContextTests
{
    [Fact]
    public void ForwardedFor_FirstEntryWins()
    {
        var c = new DefaultHttpContext();
        c.Request.Headers["X-Forwarded-For"] = "203.0.113.9, 10.0.0.1";
        Assert.Equal("203.0.113.9", RiskRequestContext.ResolveIp(c));
    }

    [Fact]
    public void InvalidForwardedFor_ReturnsNull_SoAdminApiNeverGets400()
    {
        var c = new DefaultHttpContext();
        c.Request.Headers["X-Forwarded-For"] = "not-an-ip";
        Assert.Null(RiskRequestContext.ResolveIp(c));
    }

    [Fact]
    public void WithoutHeader_UsesRemoteAddress()
    {
        var c = new DefaultHttpContext();
        c.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");
        Assert.Equal("198.51.100.7", RiskRequestContext.ResolveIp(c));
    }

    [Fact]
    public void Loopback_IsNormalizedTo127001()
    {
        var c = new DefaultHttpContext();
        c.Connection.RemoteIpAddress = IPAddress.IPv6Loopback;
        Assert.Equal("127.0.0.1", RiskRequestContext.ResolveIp(c));
    }

    [Fact]
    public void Ipv4MappedIpv6_IsUnwrapped()
    {
        var c = new DefaultHttpContext();
        c.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.5");
        Assert.Equal("203.0.113.5", RiskRequestContext.ResolveIp(c));
    }

    [Fact]
    public void NoIpInformation_ReturnsNull()
        => Assert.Null(RiskRequestContext.ResolveIp(new DefaultHttpContext()));

    [Fact]
    public void LongDeviceFingerprint_IsTruncatedTo128()
    {
        var c = TestHttp.Browser(h => h["X-Device-Fp"] = new string('a', 200));
        Assert.Equal(128, RiskRequestContext.From(c).DeviceFp!.Length);
    }

    [Fact]
    public void EmptyHeaders_BecomeNull()
    {
        var c = TestHttp.Browser(h => { h["X-Device-Fp"] = "   "; h["X-Session-Id"] = ""; });
        var rc = RiskRequestContext.From(c);
        Assert.Null(rc.DeviceFp);
        Assert.Null(rc.SessionId);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("x", null)]
    [InlineData("", null)]
    public void HadInput_IsParsedFromHeader(string header, bool? expected)
    {
        var c = TestHttp.Browser(h => h["X-Had-Input"] = header);
        Assert.Equal(expected, RiskRequestContext.From(c).HadInput);
    }

    [Fact]
    public void WebdriverFlag_IsParsedFromHeader()
    {
        Assert.True(RiskRequestContext.From(TestHttp.Browser(h => h["X-Webdriver"] = "1")).Webdriver);
        Assert.False(RiskRequestContext.From(TestHttp.Browser(h => h["X-Webdriver"] = "0")).Webdriver);
    }
}
