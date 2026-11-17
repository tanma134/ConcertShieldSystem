using QueueAPI.Models;

namespace QueueAPI.Repositories;

public interface IQueueRepository
{
    Task<QueueConfiguration?> GetConfigurationByEventIdAsync(int eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueueConfiguration>> GetConfigurationsEndedBeforeAsync(
        DateTime time,
        CancellationToken cancellationToken = default);
    Task AddConfigurationAsync(QueueConfiguration configuration, CancellationToken cancellationToken = default);

    Task<QueueSession?> GetSessionAsync(int eventId, int userId, CancellationToken cancellationToken = default);
    Task<QueueSession?> GetSessionByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<QueueSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueueSession>> GetAdminSessionsAsync(
        int? eventId,
        DateTime? updatedAtFrom,
        DateTime? updatedAtBefore,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueueSession>> GetAdmittedSessionsAsync(int eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueueSession>> GetSyncableSessionsAsync(int eventId, CancellationToken cancellationToken = default);
    Task AddSessionAsync(QueueSession session, CancellationToken cancellationToken = default);

    Task<bool> AbuseAlertExistsAsync(string sessionId, string alertType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AbuseAlert>> GetAdminAlertsAsync(
        IReadOnlyCollection<string> supportedAlertTypes,
        IReadOnlyCollection<string> supportedSeverities,
        string? severity,
        string? status,
        CancellationToken cancellationToken = default);
    Task<AbuseAlert?> GetAdminAlertByIdAsync(
        long alertId,
        IReadOnlyCollection<string> supportedAlertTypes,
        IReadOnlyCollection<string> supportedSeverities,
        CancellationToken cancellationToken = default);
    Task<AbuseAlert?> GetRelatedAlertAsync(
        string sessionId,
        long? alertId,
        IReadOnlyCollection<string> supportedAlertTypes,
        IReadOnlyCollection<string> supportedSeverities,
        CancellationToken cancellationToken = default);
    Task AddAbuseAlertAsync(AbuseAlert alert, CancellationToken cancellationToken = default);
    void DetachAbuseAlert(AbuseAlert alert);

    Task<int> ExpireAdmittedSessionAsync(
        long queueSessionId,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
