namespace QueueAPI.Services;

public interface IQueueAuditLogClient
{
    Task LogAsync(
        int? userId,
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        string? ipAddress,
        string? requestId,
        string? userAgent);
}
