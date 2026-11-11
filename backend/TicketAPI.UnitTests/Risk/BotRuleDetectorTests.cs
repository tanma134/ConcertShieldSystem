using Microsoft.AspNetCore.Http;
using TicketAPI.Risk;
using Xunit;

namespace TicketAPI.Tests;

public class BotRuleDetectorTests
{
    private static BotDetection Run(HttpContext http, SlidingCounter? counters = null, bool booking = false)
        => BotRuleDetector.Detect(RiskRequestContext.From(http), counters ?? new SlidingCounter(), TestHttp.Config(), booking);

    private static string[] Codes(BotDetection d) => d.Hits.Select(h => h.Code).ToArray();

    // ------------------------------------------------------------ request sạch

    [Fact]
    public void RealBrowserRequest_HasNoBotRules()
    {
        var d = Run(TestHttp.Browser(), booking: true);

        Assert.Empty(d.Hits);
        Assert.Empty(d.Missing);
    }

    // ------------------------------------------------------------ nhóm H (header)

    [Fact]
    public void BareCurl_TriggersH01_H03_H04_ButNotH02()
    {
        var codes = Codes(Run(TestHttp.Curl()));

        Assert.Contains("BOT_H01", codes);   // UA là công cụ dòng lệnh
        Assert.Contains("BOT_H03", codes);   // thiếu Accept-Language / Accept-Encoding
        Assert.Contains("BOT_H04", codes);   // không có Origin/Referer
        Assert.DoesNotContain("BOT_H02", codes);   // UA không tự nhận là Chrome/Edge
    }

    [Theory]
    [InlineData("python-requests/2.31.0")]
    [InlineData("Go-http-client/1.1")]
    [InlineData("PostmanRuntime/7.36.0")]
    [InlineData("okhttp/4.12.0")]
    [InlineData("Wget/1.21")]
    public void CliUserAgents_TriggerH01(string ua)
        => Assert.Contains("BOT_H01", Codes(Run(TestHttp.Browser(h => h["User-Agent"] = ua))));

    [Fact]
    public void EmptyUserAgent_TriggersH01()
    {
        var d = Run(TestHttp.Browser(h => h["User-Agent"] = ""));
        var hit = Assert.Single(d.Hits, x => x.Code == "BOT_H01");
        Assert.Contains("empty", hit.Value);
    }

    [Fact]
    public void ChromeUserAgentWithoutSecHeaders_TriggersH02()
    {
        var d = Run(TestHttp.Browser(h => { h.Remove("sec-ch-ua"); h.Remove("sec-fetch-site"); }));
        Assert.Contains("BOT_H02", Codes(d));
    }

    [Fact]
    public void MissingAcceptLanguage_TriggersH03()
        => Assert.Contains("BOT_H03", Codes(Run(TestHttp.Browser(h => h.Remove("Accept-Language")))));

    [Fact]
    public void ForeignOrigin_TriggersH04()
        => Assert.Contains("BOT_H04", Codes(Run(TestHttp.Browser(h => h["Origin"] = "https://evil.example"))));

    [Fact]
    public void RefererFromOurDomain_IsAcceptedWhenOriginMissing()
    {
        var d = Run(TestHttp.Browser(h => { h.Remove("Origin"); h["Referer"] = "http://localhost:5173/events/1"; }));
        Assert.DoesNotContain("BOT_H04", Codes(d));
    }

    [Fact]
    public void AllowedOrigins_CanBeConfigured()
    {
        var cfg = TestHttp.Config(("Risk:AllowedOrigins:0", "https://shop.example"));
        var http = TestHttp.Browser(h => h["Origin"] = "https://shop.example");

        var d = BotRuleDetector.Detect(RiskRequestContext.From(http), new SlidingCounter(), cfg, false);
        Assert.DoesNotContain("BOT_H04", Codes(d));
    }

    // ------------------------------------------------------------ nhóm E (môi trường)

    [Fact]
    public void WebdriverHeader_TriggersE01()
        => Assert.Contains("BOT_E01", Codes(Run(TestHttp.Browser(h => h["X-Webdriver"] = "1"))));

    [Theory]
    [InlineData("Mozilla/5.0 HeadlessChrome/126.0")]
    [InlineData("Mozilla/5.0 PhantomJS/2.1")]
    [InlineData("Mozilla/5.0 (Playwright)")]
    public void HeadlessUserAgents_TriggerE01(string ua)
        => Assert.Contains("BOT_E01", Codes(Run(TestHttp.Browser(h => h["User-Agent"] = ua))));

    [Fact]
    public void NoUserInput_OnBooking_TriggersE03_ButNotOnOtherRequests()
    {
        var http = TestHttp.Browser(h => h["X-Had-Input"] = "0");

        Assert.Contains("BOT_E03", Codes(Run(http, booking: true)));
        Assert.DoesNotContain("BOT_E03", Codes(Run(http, booking: false)));
    }

    [Fact]
    public void UnknownInputHeader_OnBooking_IsReportedAsMissingSignal_NotAsHit()
    {
        var d = Run(TestHttp.Browser(h => h.Remove("X-Had-Input")), booking: true);

        Assert.DoesNotContain("BOT_E03", Codes(d));
        Assert.Contains("had_input", d.Missing);
    }

    [Fact]
    public void MissingDeviceFingerprint_IsReportedAsMissingSignal()
        => Assert.Contains("device_fp", Run(TestHttp.Browser(h => h.Remove("X-Device-Fp"))).Missing);

    // ------------------------------------------------------------ nhóm V (tốc độ)

    [Fact]
    public void V01_Level1After50Requests_Level2After100()
    {
        var counters = new SlidingCounter();
        var http = TestHttp.Browser();

        BotDetection last = Run(http, counters);
        for (var i = 2; i <= 50; i++) last = Run(http, counters);
        Assert.DoesNotContain("BOT_V01", Codes(last));          // 50 request/10s = 5 req/s: chưa vượt

        last = Run(http, counters);                              // request thứ 51
        Assert.Equal(1, Assert.Single(last.Hits, h => h.Code == "BOT_V01").Level);

        for (var i = 52; i <= 101; i++) last = Run(http, counters);   // request thứ 101
        Assert.Equal(2, Assert.Single(last.Hits, h => h.Code == "BOT_V01").Level);
    }

    [Fact]
    public void V01_CountsPerSession()
    {
        var counters = new SlidingCounter();
        var a = TestHttp.Browser(h => h["X-Session-Id"] = "A");
        var b = TestHttp.Browser(h => h["X-Session-Id"] = "B");

        for (var i = 0; i < 60; i++) Run(a, counters);

        Assert.DoesNotContain("BOT_V01", Codes(Run(b, counters)));
    }

    [Fact]
    public void V04_MoreThan30BookingsPerMinuteFromOneIp()
    {
        var counters = new SlidingCounter();
        var http = TestHttp.Browser();

        BotDetection last = Run(http, counters, booking: true);
        for (var i = 2; i <= 30; i++) last = Run(http, counters, booking: true);
        Assert.DoesNotContain("BOT_V04", Codes(last));

        last = Run(http, counters, booking: true);               // lượt thứ 31
        Assert.Contains("BOT_V04", Codes(last));
    }

    [Fact]
    public void V04_IsOnlyCountedForBookingRequests()
    {
        var counters = new SlidingCounter();
        var http = TestHttp.Browser();

        BotDetection last = Run(http, counters, booking: false);
        for (var i = 0; i < 40; i++) last = Run(http, counters, booking: false);

        Assert.DoesNotContain("BOT_V04", Codes(last));
    }
}
