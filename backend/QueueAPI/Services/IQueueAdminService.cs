using QueueAPI.DTOs;

namespace QueueAPI.Services;

public interface IQueueAdminService
{
    Task<IReadOnlyList<QueueAdminSessionResponse>> GetSessionsAsync(
        int? eventId,
        DateOnly? fromDate,
        DateOnly? toDate);
    Task<QueueAdminResult<string>> RevokeTokenAsync(string token);
    Task<QueueAdminResult<string>> RevokeSessionAsync(string sessionId);
    Task<QueueAdminResult<string>> UnrevokeSessionAsync(string sessionId);
    Task<IReadOnlyList<AbuseAlertResponse>> GetAlertsAsync(string? severity, string? status);
    Task<AbuseAlertResponse?> GetAlertAsync(long alertId);
    Task<QueueAdminResult<QueueAdminSessionActionResponse>> UnblockSessionAsync(
        string sessionId,
        long? alertId,
        QueueAdminAuditContext auditContext);
    Task<QueueAdminResult<QueueAdminSessionActionResponse>> RecordSessionActionAsync(
        string sessionId,
        string status,
        long? alertId,
        QueueAdminAuditContext auditContext);
}

public enum QueueAdminResultStatus
{
    Success,
    NotFound,
    Conflict
}

public sealed record QueueAdminResult<T>(
    QueueAdminResultStatus Status,
    T? Value = default,
    string? ErrorCode = null)
{
    public static QueueAdminResult<T> Success(T value) =>
        new(QueueAdminResultStatus.Success, value);

    public static QueueAdminResult<T> NotFound(string? errorCode = null) =>
        new(QueueAdminResultStatus.NotFound, ErrorCode: errorCode);

    public static QueueAdminResult<T> Conflict(string errorCode) =>
        new(QueueAdminResultStatus.Conflict, ErrorCode: errorCode);
}

public sealed record QueueAdminSessionActionResponse(string Status, string SessionStatus);

public sealed record QueueAdminAuditContext(
    int UserId,
    string? IpAddress,
    string RequestId,
    string UserAgent);
