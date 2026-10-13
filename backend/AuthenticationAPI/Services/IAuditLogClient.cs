namespace AuthenticationAPI.Services
{
    public interface IAuditLogClient
    {
        Task LogAsync(int? userId, string actorType, string action, string entityType,
            string? entityId = null, object? oldValue = null, object? newValue = null,
            string? ipAddress = null, string? userAgent = null);
    }
}