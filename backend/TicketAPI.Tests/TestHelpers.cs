using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace TicketAPI.Tests;

internal static class TestHttp
{
    /// <summary>Request giống trình duyệt thật: đủ header, không rule bot nào khớp.</summary>
    public static DefaultHttpContext Browser(Action<IHeaderDictionary>? tweak = null)
    {
        var ctx = new DefaultHttpContext();
        var h = ctx.Request.Headers;
        h["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
        h["sec-ch-ua"] = "\"Chromium\";v=\"126\", \"Google Chrome\";v=\"126\"";
        h["sec-fetch-site"] = "same-site";
        h["Accept-Language"] = "vi-VN,vi;q=0.9";
        h["Accept-Encoding"] = "gzip, deflate, br";
        h["Origin"] = "http://localhost:5173";
        h["X-Device-Fp"] = "fp_test";
        h["X-Session-Id"] = "sess-1";
        h["X-Webdriver"] = "0";
        h["X-Had-Input"] = "1";
        h["X-Forwarded-For"] = "203.0.113.9";
        tweak?.Invoke(h);
        return ctx;
    }

    /// <summary>Request kiểu `curl` trần: chỉ có User-Agent.</summary>
    public static DefaultHttpContext Curl(Action<IHeaderDictionary>? tweak = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["User-Agent"] = "curl/8.4.0";
        tweak?.Invoke(ctx.Request.Headers);
        return ctx;
    }

    public static IConfiguration Config(params (string Key, string Value)[] items)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(items.Select(i => new KeyValuePair<string, string?>(i.Key, i.Value)))
            .Build();
}
