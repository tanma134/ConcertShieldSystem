using Microsoft.EntityFrameworkCore;
using QueueAPI.Data;
using QueueAPI.Models;

namespace QueueAPI.Repositories;

public sealed class QueueRepository(QueueDbContext db) : IQueueRepository
{
    public Task<QueueConfiguration?> GetConfigurationByEventIdAsync(
        int eventId,
        CancellationToken cancellationToken = default) =>
        db.QueueConfigurations.SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);

    public async Task<IReadOnlyList<QueueConfiguration>> GetConfigurationsEndedBeforeAsync(
        DateTime time,
        CancellationToken cancellationToken = default) =>
        await db.QueueConfigurations
            .AsNoTracking()
            .Where(x => x.PreQueueEndTime <= time)
            .ToListAsync(cancellationToken);

    public Task AddConfigurationAsync(
        QueueConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        db.QueueConfigurations.AddAsync(configuration, cancellationToken).AsTask();

    public Task<QueueSession?> GetSessionAsync(
        int eventId,
        int userId,
        CancellationToken cancellationToken = default) =>
        db.QueueSessions.SingleOrDefaultAsync(
            x => x.EventId == eventId && x.UserId == userId,
            cancellationToken);

    public Task<QueueSession?> GetSessionByTokenAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        db.QueueSessions.SingleOrDefaultAsync(x => x.QueueToken == token, cancellationToken);

    public Task<QueueSession?> GetSessionByIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        db.QueueSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);

    public async Task<IReadOnlyList<QueueSession>> GetAdminSessionsAsync(
        int? eventId,
        DateTime? updatedAtFrom,
        DateTime? updatedAtBefore,
        CancellationToken cancellationToken = default)
    {
        var query = db.QueueSessions.AsNoTracking().AsQueryable();
        if (eventId.HasValue)
            query = query.Where(x => x.EventId == eventId.Value);
        if (updatedAtFrom.HasValue)
            query = query.Where(x => x.UpdatedAt >= updatedAtFrom.Value);
        if (updatedAtBefore.HasValue)
            query = query.Where(x => x.UpdatedAt < updatedAtBefore.Value);

        return await query
            .OrderByDescending(x => x.UpdatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QueueSession>> GetAdmittedSessionsAsync(
        int eventId,
        CancellationToken cancellationToken = default) =>
        await db.QueueSessions
            .Where(x => x.EventId == eventId && x.Status == "ADMITTED")
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QueueSession>> GetSyncableSessionsAsync(
        int eventId,
        CancellationToken cancellationToken = default) =>
        await db.QueueSessions
            .Where(x => x.EventId == eventId && (x.Status == "REGISTERED" || x.Status == "WAITING"))
            .ToListAsync(cancellationToken);

    public Task AddSessionAsync(
        QueueSession session,
        CancellationToken cancellationToken = default) =>
        db.QueueSessions.AddAsync(session, cancellationToken).AsTask();

    public Task<bool> AbuseAlertExistsAsync(
        string sessionId,
        string alertType,
        CancellationToken cancellationToken = default) =>
        db.AbuseAlerts.AnyAsync(
            x => x.SessionId == sessionId && x.AlertType == alertType,
            cancellationToken);

    public async Task<IReadOnlyList<AbuseAlert>> GetAdminAlertsAsync(
        IReadOnlyCollection<string> supportedAlertTypes,
        IReadOnlyCollection<string> supportedSeverities,
        string? severity,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = db.AbuseAlerts.AsNoTracking()
            .Where(x => supportedAlertTypes.Contains(x.AlertType) && supportedSeverities.Contains(x.Severity));
        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(x => x.Severity == severity);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public Task<AbuseAlert?> GetAdminAlertByIdAsync(
        long alertId,
        IReadOnlyCollection<string> supportedAlertTypes,
        IReadOnlyCollection<string> supportedSeverities,
        CancellationToken cancellationToken = default) =>
        db.AbuseAlerts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.AbuseAlertId == alertId &&
            supportedAlertTypes.Contains(x.AlertType) &&
            supportedSeverities.Contains(x.Severity), cancellationToken);

    public Task<AbuseAlert?> GetRelatedAlertAsync(
        string sessionId,
        long? alertId,
        IReadOnlyCollection<string> supportedAlertTypes,
        IReadOnlyCollection<string> supportedSeverities,
        CancellationToken cancellationToken = default)
    {
        var query = db.AbuseAlerts.Where(x =>
            x.SessionId == sessionId &&
            supportedAlertTypes.Contains(x.AlertType) &&
            supportedSeverities.Contains(x.Severity));

        return alertId.HasValue
            ? query.SingleOrDefaultAsync(x => x.AbuseAlertId == alertId.Value, cancellationToken)
            : query.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddAbuseAlertAsync(
        AbuseAlert alert,
        CancellationToken cancellationToken = default) =>
        db.AbuseAlerts.AddAsync(alert, cancellationToken).AsTask();

    public void DetachAbuseAlert(AbuseAlert alert) =>
        db.Entry(alert).State = EntityState.Detached;

    public Task<int> ExpireAdmittedSessionAsync(
        long queueSessionId,
        DateTime updatedAt,
        CancellationToken cancellationToken = default) =>
        db.QueueSessions
            .Where(x => x.QueueSessionId == queueSessionId && x.Status == "ADMITTED")
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, "EXPIRED")
                .SetProperty(x => x.UpdatedAt, updatedAt), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
