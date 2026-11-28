using System.Net;
using System.Text;
using System.Text.Json;
using TicketAPI.API;
using Xunit;

namespace TicketAPI.UnitTests.API;

public sealed class QueueAdmissionClientTests
{
    [Fact]
    public async Task EnsureAdmittedAsync_AllowsActiveAdmission()
    {
        var client = CreateClient("ADMITTED", DateTimeOffset.UtcNow.AddMinutes(2));

        await client.EnsureAdmittedAsync(42, "Bearer token", "queue-token");
    }

    [Theory]
    [InlineData("REGISTERED")]
    [InlineData("WAITING")]
    [InlineData("EXPIRED")]
    public async Task EnsureAdmittedAsync_RejectsNonAdmittedStatus(string status)
    {
        var client = CreateClient(status, DateTimeOffset.UtcNow.AddMinutes(2));

        await Assert.ThrowsAsync<QueueAdmissionRequiredException>(
            () => client.EnsureAdmittedAsync(42, "Bearer token", "queue-token"));
    }

    [Fact]
    public async Task EnsureAdmittedAsync_RejectsExpiredOrMissingAdmissionExpiry()
    {
        var expiredClient = CreateClient("ADMITTED", DateTimeOffset.UtcNow.AddMinutes(-1));
        var missingExpiryClient = CreateClient("ADMITTED", null);

        await Assert.ThrowsAsync<QueueAdmissionRequiredException>(
            () => expiredClient.EnsureAdmittedAsync(42, "Bearer token", "queue-token"));
        await Assert.ThrowsAsync<QueueAdmissionRequiredException>(
            () => missingExpiryClient.EnsureAdmittedAsync(42, "Bearer token", "queue-token"));
    }

    private static QueueAdmissionClient CreateClient(string status, DateTimeOffset? admissionExpiresAt)
    {
        var payload = JsonSerializer.Serialize(
            new QueueStatus(status, admissionExpiresAt),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new StubHttpMessageHandler(payload);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://queue.test/")
        };

        return new QueueAdmissionClient(http);
    }

    private sealed class StubHttpMessageHandler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
    }
}
