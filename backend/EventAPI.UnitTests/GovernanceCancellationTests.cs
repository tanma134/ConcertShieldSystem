using System.Text.Json;
using EventAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace EventAPI.UnitTests;
public class GovernanceCancellationTests
{
    [Fact]
    public async Task ClientAbortDoesNotWriteJsonIntoDisconnectedResponse()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var context = new DefaultHttpContext { RequestAborted = source.Token };
        context.Response.Body = new MemoryStream();
        var middleware = new GovernanceMiddleware(_ => throw new OperationCanceledException(source.Token), NullLogger<GovernanceMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(499, context.Response.StatusCode); Assert.Equal(0, context.Response.Body.Length);
    }
    [Fact]
    public async Task StorageTimeoutReturns504WithErrorCodeAndTrace()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "test-trace" };
        context.Response.Body = new MemoryStream();
        var middleware = new GovernanceMiddleware(_ => throw new AssetUploadException("test.pdf", "StorageTimeout"), NullLogger<GovernanceMiddleware>.Instance);
        await middleware.InvokeAsync(context); Assert.Equal(504, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("StorageTimeout", json.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal("test-trace", json.RootElement.GetProperty("traceId").GetString());
    }
    [Fact]
    public async Task InternalCancellationWithoutCallerAbortReturnsTimeout()
    {
        var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
        var middleware = new GovernanceMiddleware(_ => throw new TaskCanceledException(), NullLogger<GovernanceMiddleware>.Instance);
        await middleware.InvokeAsync(context); Assert.Equal(504, context.Response.StatusCode);
    }
    [Fact]
    public async Task AuthenticationFailureStillReturns503WithSafeMessage()
    {
        var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
        var middleware = new GovernanceMiddleware(_ => throw new AssetUploadException("test.pdf", "StorageAuthentication"), NullLogger<GovernanceMiddleware>.Instance);
        await middleware.InvokeAsync(context); Assert.Equal(503, context.Response.StatusCode);
    }
}
