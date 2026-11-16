using System.Net.Http.Json;

namespace QueueAPI.Services;

public sealed class QueueAuditLogClient(HttpClient http, ILogger<QueueAuditLogClient> logger) : IQueueAuditLogClient
{
    public async Task LogAsync(
        int? userId,
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        string? ipAddress,
        string? requestId,
        string? userAgent)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("/api/internal/audit-logs", new
            {
                userId,
                actorType = "admin",
                action,
                entityType,
                entityId,
                oldValue,
                newValue,
                ipAddress,
                requestId,
                userAgent
            });
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Queue audit log write returned {Status} for action {Action}", response.StatusCode, action);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Failed to write queue audit log for action {Action}", action);
        }
        catch (TaskCanceledException exception)
        {
            logger.LogWarning(exception, "Queue audit log request timed out for action {Action}", action);
        }
    }
}
