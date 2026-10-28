using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TicketAPI.API;

public sealed record QueueStatus(string Status, DateTimeOffset? AdmissionExpiresAt);

public sealed class QueueAdmissionClient(HttpClient http)
{
    public async Task EnsureAdmittedAsync(
        int eventId,
        string? authorization,
        string? queueToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueToken))
            throw new QueueAdmissionRequiredException();
        if (string.IsNullOrWhiteSpace(authorization))
            throw new UnauthorizedAccessException("Please sign in again before purchasing tickets.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/queue/{eventId}/status");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("X-Queue-Token", queueToken);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new QueueApiUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new QueueApiUnavailableException(ex);
        }

        using (response)
        {
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Conflict)
                throw new QueueAdmissionRequiredException();
            if (!response.IsSuccessStatusCode)
                throw new QueueApiUnavailableException();

            QueueStatus? status;
            try
            {
                status = await response.Content.ReadFromJsonAsync<QueueStatus>(cancellationToken: cancellationToken);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new QueueApiUnavailableException(ex);
            }

            if (status?.Status != "ADMITTED" ||
                status.AdmissionExpiresAt is not { } expiresAt ||
                expiresAt <= DateTimeOffset.UtcNow)
            {
                throw new QueueAdmissionRequiredException();
            }
        }
    }
}

public sealed class QueueAdmissionRequiredException()
    : Exception("Queue admission is required or has expired. Return to the queue and wait for admission.");

public sealed class QueueApiUnavailableException(Exception? innerException = null)
    : Exception("Could not verify queue admission. Please try again.", innerException);
