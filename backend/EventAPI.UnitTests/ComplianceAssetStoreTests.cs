using System.Net;
using System.Text;
using System.Text.Json;
using EventAPI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace EventAPI.UnitTests;

public class ComplianceAssetStoreTests
{
    private const string PublicId = "concertshield/compliance/test.pdf";
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private static IConfiguration Config(bool complete = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Cloudinary:CloudName"] = " test-cloud ", ["Cloudinary:ApiKey"] = " test-key ",
        ["Cloudinary:ApiSecret"] = complete ? " test-secret " : " "
    }).Build();
    private static ComplianceAssetStore Store(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, bool complete = true)
        => new(new HttpClient(new Handler(send)), Config(complete), NullLogger<ComplianceAssetStore>.Instance);
    private static HttpResponseMessage Response(HttpStatusCode status, object body)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    private static object Success(string type = "authenticated", string resource = "raw")
        => new { public_id = PublicId, secure_url = "https://res.cloudinary.com/test-cloud/raw/authenticated/test.pdf", type, resource_type = resource };

    [Fact]
    public async Task UploadSendsAuthenticatedRawPdfAndReturnsAsset()
    {
        var store = Store(async (request, ct) =>
        {
            Assert.Equal("https://api.cloudinary.com/v1_1/test-cloud/raw/upload", request.RequestUri!.AbsoluteUri);
            var parts = Assert.IsType<MultipartFormDataContent>(request.Content).ToList();
            string Name(HttpContent c) => c.Headers.ContentDisposition!.Name!.Trim('"');
            Assert.Equal("authenticated", await parts.Single(p => Name(p) == "type").ReadAsStringAsync(ct));
            Assert.Equal(PublicId, await parts.Single(p => Name(p) == "public_id").ReadAsStringAsync(ct));
            Assert.Equal("test-key", await parts.Single(p => Name(p) == "api_key").ReadAsStringAsync(ct));
            Assert.Equal("application/pdf", parts.Single(p => Name(p) == "file").Headers.ContentType!.MediaType);
            Assert.Equal("%PDF-demo", await parts.Single(p => Name(p) == "file").ReadAsStringAsync(ct));
            Assert.Matches("^[a-f0-9]{40}$", await parts.Single(p => Name(p) == "signature").ReadAsStringAsync(ct));
            return Response(HttpStatusCode.OK, Success());
        });
        var asset = await store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, default);
        Assert.Equal(PublicId, asset.PublicId);
    }
    [Theory]
    [InlineData(401, "StorageAuthentication")]
    [InlineData(403, "StorageAuthentication")]
    [InlineData(413, "StorageFileTooLarge")]
    [InlineData(429, "StorageRateLimit")]
    [InlineData(500, "StorageRejected")]
    public async Task RejectedResponseKeepsStatusAndSafeReason(int status, string code)
    {
        var store = Store((r, ct) => Task.FromResult(Response((HttpStatusCode)status, new { error = new { message = "Upload refused test-secret test-key" } })));
        var error = await Assert.ThrowsAsync<AssetUploadException>(() => store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, default));
        Assert.Equal(code, error.ErrorCode); Assert.Equal(status, error.ProviderStatus); Assert.Equal(PublicId, error.PublicId);
        Assert.NotNull(error.InnerException); Assert.Contains("Upload refused", error.InnerException!.Message);
        Assert.DoesNotContain("test-secret", error.ToString()); Assert.DoesNotContain("test-key", error.ToString());
    }
    [Fact]
    public async Task SignatureFailureIsClassifiedAndRedacted()
    {
        var store = Store((r, ct) => Task.FromResult(Response(HttpStatusCode.BadRequest,
            new { error = new { message = "Invalid Signature " + new string('a', 40) } })));
        var error = await Assert.ThrowsAsync<AssetUploadException>(() => store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, default));
        Assert.Equal("StorageAuthentication", error.ErrorCode); Assert.DoesNotContain(new string('a', 40), error.ToString());
    }
    [Fact]
    public async Task MissingCredentialsFailWithoutSendingRequest()
    {
        var calls = 0; var store = Store((r, ct) => { calls++; return Task.FromResult(Response(HttpStatusCode.OK, Success())); }, false);
        var error = await Assert.ThrowsAsync<AssetUploadException>(() => store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, default));
        Assert.Equal("StorageConfiguration", error.ErrorCode); Assert.Equal(0, calls);
    }
    [Fact]
    public async Task NetworkFailurePreservesInnerException()
    {
        var store = Store((r, ct) => throw new HttpRequestException("Network unavailable"));
        var error = await Assert.ThrowsAsync<AssetUploadException>(() => store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, default));
        Assert.Equal("StorageConnection", error.ErrorCode); Assert.IsType<HttpRequestException>(error.InnerException);
    }
    [Fact]
    public async Task TimeoutHasOwnCode()
    {
        var store = Store((r, ct) => throw new TaskCanceledException());
        var error = await Assert.ThrowsAsync<AssetUploadException>(() => store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, default));
        Assert.Equal("StorageTimeout", error.ErrorCode);
    }
    [Fact]
    public async Task CallerCancellationIsNotTurnedIntoStorageFailure()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var store = Store((r, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(Response(HttpStatusCode.OK, Success())); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, source.Token));
    }
    [Theory]
    [InlineData("upload", "raw")]
    [InlineData("authenticated", "image")]
    public async Task PublicOrWrongResourceIsNotAcceptedAsPrivateCompliance(string type, string resource)
    {
        var store = Store((r, ct) => Task.FromResult(Response(HttpStatusCode.OK, Success(type, resource))));
        var error = await Assert.ThrowsAsync<AssetUploadException>(() => store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, default));
        Assert.Equal("StorageInvalidResponse", error.ErrorCode);
    }
    [Fact]
    public async Task InvalidSuccessJsonIsNotStored()
    {
        var store = Store((r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not JSON") }));
        var error = await Assert.ThrowsAsync<AssetUploadException>(() => store.UploadAsync("%PDF-demo"u8.ToArray(), PublicId, default));
        Assert.Equal("StorageInvalidResponse", error.ErrorCode);
    }
}
