using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using TicketAPI.Risk;
using Xunit;

namespace TicketAPI.Tests;

public class ChallengeVerifierTests
{
    private const string JwtKey = "unit-test-signing-key-at-least-32-bytes-long!!";
    private const string Issuer = "AuthenticationAPI";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respond;
        public int Calls { get; private set; }
        public StubHandler(Func<HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_respond());
        }
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static (ChallengeVerifier Verifier, StubHandler Handler) Create(
        Func<HttpResponseMessage> turnstile, params (string, string)[] config)
    {
        var handler = new StubHandler(turnstile);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        var verifier = new ChallengeVerifier(
            factory.Object,
            new MemoryCache(new MemoryCacheOptions()),
            TestHttp.Config(config),
            NullLogger<ChallengeVerifier>.Instance);
        return (verifier, handler);
    }

    private static DefaultHttpContext WithChallengeToken(string? token)
        => TestHttp.Browser(h => { if (token != null) h["X-Challenge-Token"] = token; });

    private static DefaultHttpContext WithStepUp(string? token)
        => TestHttp.Browser(h => { if (token != null) h["X-StepUp-Token"] = token; });

    private static string StepUpJwt(
        int userId = 5,
        string purpose = ChallengeVerifier.StepUpPurpose,
        string audience = ChallengeVerifier.StepUpAudience,
        string key = JwtKey,
        TimeSpan? notBefore = null,
        TimeSpan? expires = null)
    {
        var now = DateTime.UtcNow;
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            Issuer, audience,
            new[] { new Claim("sub", userId.ToString()), new Claim("purpose", purpose) },
            now + (notBefore ?? TimeSpan.FromSeconds(-1)),
            now + (expires ?? TimeSpan.FromMinutes(10)),
            creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    // ------------------------------------------------------------ CAPTCHA

    [Fact]
    public async Task Captcha_WithoutToken_IsNotPassed_AndDoesNotCallTurnstile()
    {
        var (v, handler) = Create(() => Json("{\"success\":true}"), ("Turnstile:SecretKey", "secret"));

        var ok = await v.IsPassedAsync(WithChallengeToken(null), "CAPTCHA", 5);

        Assert.False(ok);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Captcha_WithoutSecretKeyConfigured_IsNotPassed()
    {
        var (v, handler) = Create(() => Json("{\"success\":true}"));

        var ok = await v.IsPassedAsync(WithChallengeToken("tok"), "CAPTCHA", 5);

        Assert.False(ok);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Captcha_TurnstileSuccess_IsPassed()
    {
        var (v, handler) = Create(() => Json("{\"success\":true}"), ("Turnstile:SecretKey", "secret"));

        var ok = await v.IsPassedAsync(WithChallengeToken("tok"), "CAPTCHA", 5);

        Assert.True(ok);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Captcha_TurnstileRejects_IsNotPassed()
    {
        var (v, _) = Create(() => Json("{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}"),
            ("Turnstile:SecretKey", "secret"));

        Assert.False(await v.IsPassedAsync(WithChallengeToken("bad"), "CAPTCHA", 5));
    }

    [Fact]
    public async Task Captcha_FailedAttempt_IsNotCached()
    {
        var success = false;
        var (v, handler) = Create(() => Json(success ? "{\"success\":true}" : "{\"success\":false}"),
            ("Turnstile:SecretKey", "secret"));

        Assert.False(await v.IsPassedAsync(WithChallengeToken("t1"), "CAPTCHA", 5));
        success = true;
        Assert.True(await v.IsPassedAsync(WithChallengeToken("t2"), "CAPTCHA", 5));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Captcha_AfterPassing_SameSessionIsRemembered_WithoutNewToken()
    {
        var (v, handler) = Create(() => Json("{\"success\":true}"), ("Turnstile:SecretKey", "secret"));

        Assert.True(await v.IsPassedAsync(WithChallengeToken("tok"), "CAPTCHA", 5));
        Assert.True(await v.IsPassedAsync(WithChallengeToken(null), "CAPTCHA", 5));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Captcha_PassIsNotSharedWithAnotherSession()
    {
        var (v, _) = Create(() => Json("{\"success\":true}"), ("Turnstile:SecretKey", "secret"));

        Assert.True(await v.IsPassedAsync(WithChallengeToken("tok"), "CAPTCHA", 5));

        var other = TestHttp.Browser(h => h["X-Session-Id"] = "sess-other");
        Assert.False(await v.IsPassedAsync(other, "CAPTCHA", 5));
    }

    [Fact]
    public async Task Captcha_TurnstileUnreachable_IsNotPassed()
    {
        var (v, _) = Create(() => throw new HttpRequestException("down"), ("Turnstile:SecretKey", "secret"));

        Assert.False(await v.IsPassedAsync(WithChallengeToken("tok"), "CAPTCHA", 5));
    }

    [Fact]
    public async Task Captcha_MalformedTurnstileResponse_IsNotPassed()
    {
        var (v, _) = Create(() => Json("not json"), ("Turnstile:SecretKey", "secret"));

        Assert.False(await v.IsPassedAsync(WithChallengeToken("tok"), "CAPTCHA", 5));
    }

    // ------------------------------------------------------------ OTP (step-up JWT)

    private static (ChallengeVerifier Verifier, StubHandler Handler) OtpVerifier()
        => Create(() => Json("{\"success\":true}"), ("JwtSettings:SecretKey", JwtKey));

    [Fact]
    public async Task Otp_ValidStepUpToken_IsPassed()
    {
        var (v, handler) = OtpVerifier();

        Assert.True(await v.IsPassedAsync(WithStepUp(StepUpJwt(userId: 5)), "OTP", 5));
        Assert.Equal(0, handler.Calls); // OTP never calls Turnstile
    }

    [Fact]
    public async Task Otp_ChallengeTypeIsCaseInsensitive()
    {
        var (v, _) = OtpVerifier();

        Assert.True(await v.IsPassedAsync(WithStepUp(StepUpJwt(userId: 5)), "otp", 5));
    }

    [Fact]
    public async Task Otp_NoToken_IsNotPassed()
    {
        var (v, _) = OtpVerifier();

        Assert.False(await v.IsPassedAsync(WithStepUp(null), "OTP", 5));
    }

    [Fact]
    public async Task Otp_AnonymousUser_IsNotPassed()
    {
        var (v, _) = OtpVerifier();

        Assert.False(await v.IsPassedAsync(WithStepUp(StepUpJwt(userId: 5)), "OTP", null));
    }

    [Fact]
    public async Task Otp_TokenBelongingToAnotherUser_IsNotPassed()
    {
        var (v, _) = OtpVerifier();

        Assert.False(await v.IsPassedAsync(WithStepUp(StepUpJwt(userId: 99)), "OTP", 5));
    }

    [Fact]
    public async Task Otp_ExpiredToken_IsNotPassed()
    {
        var (v, _) = OtpVerifier();
        var expired = StepUpJwt(notBefore: TimeSpan.FromMinutes(-20), expires: TimeSpan.FromMinutes(-10));

        Assert.False(await v.IsPassedAsync(WithStepUp(expired), "OTP", 5));
    }

    [Fact]
    public async Task Otp_WrongPurpose_IsNotPassed()
    {
        var (v, _) = OtpVerifier();

        Assert.False(await v.IsPassedAsync(WithStepUp(StepUpJwt(purpose: "access")), "OTP", 5));
    }

    [Fact]
    public async Task Otp_WrongAudience_IsNotPassed()
    {
        var (v, _) = OtpVerifier();

        Assert.False(await v.IsPassedAsync(WithStepUp(StepUpJwt(audience: "SomeOtherApi")), "OTP", 5));
    }

    [Fact]
    public async Task Otp_TokenSignedWithDifferentKey_IsNotPassed()
    {
        var (v, _) = OtpVerifier();
        var forged = StepUpJwt(key: "another-signing-key-also-32-bytes-or-more!!!");

        Assert.False(await v.IsPassedAsync(WithStepUp(forged), "OTP", 5));
    }

    [Fact]
    public async Task Otp_GarbageToken_IsNotPassed()
    {
        var (v, _) = OtpVerifier();

        Assert.False(await v.IsPassedAsync(WithStepUp("not.a.jwt"), "OTP", 5));
    }

    [Fact]
    public async Task Otp_WithoutJwtKeyConfigured_IsNotPassed()
    {
        var (v, _) = Create(() => Json("{\"success\":true}"));

        Assert.False(await v.IsPassedAsync(WithStepUp(StepUpJwt()), "OTP", 5));
    }
}
