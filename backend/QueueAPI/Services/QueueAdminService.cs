using QueueAPI.DTOs;
using QueueAPI.Models;
using QueueAPI.Repositories;
using QueueAPI.Time;

namespace QueueAPI.Services;

public sealed class QueueAdminService(
    IQueueRepository repository,
    IRedisQueueStore redis,
    IQueueAuditLogClient auditLog) : IQueueAdminService
{
    private static readonly string[] SupportedAlertTypes =
    [
        "MULTIPLE_REGISTRATION_ATTEMPTS",
        "AUTOMATED_REQUEST_PATTERN",
        "TOKEN_REPLAY"
    ];

    private static readonly string[] SupportedSeverities = ["LOW", "MEDIUM", "HIGH"];

    public async Task<IReadOnlyList<QueueAdminSessionResponse>> GetSessionsAsync(
        int? eventId,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        var updatedAtFrom = fromDate?.ToDateTime(TimeOnly.MinValue);
        DateTime? updatedAtBefore = toDate.HasValue && toDate.Value < DateOnly.MaxValue
            ? toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : null;
        var sessions = await repository.GetAdminSessionsAsync(eventId, updatedAtFrom, updatedAtBefore);
        return sessions.Select(x => new QueueAdminSessionResponse(
            x.QueueSessionId,
            x.EventId,
            x.UserId,
            x.SessionId,
            x.Status,
            x.Position,
            x.QueueToken[..8] + "...",
            QueueTimeZone.AsVietnamOffset(x.UpdatedAt))).ToArray();
    }

    public async Task<QueueAdminResult<string>> RevokeTokenAsync(string token)
    {
        var session = await repository.GetSessionByTokenAsync(token);
        if (session == null)
            return QueueAdminResult<string>.NotFound("TOKEN_INVALID");

        var config = await repository.GetConfigurationByEventIdAsync(session.EventId);
        if (config == null)
            return QueueAdminResult<string>.NotFound("EVENT_NOT_FOUND");

        await redis.RevokeAsync(
            session.EventId,
            session.UserId,
            session.SessionId,
            token,
            CycleKey(config),
            CancellationToken.None);
        session.Status = "REVOKED";
        session.UpdatedAt = QueueTimeZone.Now;
        await repository.SaveChangesAsync();
        return QueueAdminResult<string>.Success("Queue token revoked successfully.");
    }

    public async Task<QueueAdminResult<string>> RevokeSessionAsync(string sessionId)
    {
        var session = await repository.GetSessionByIdAsync(sessionId);
        if (session == null)
            return QueueAdminResult<string>.NotFound("TOKEN_INVALID");

        var config = await repository.GetConfigurationByEventIdAsync(session.EventId);
        if (config == null)
            return QueueAdminResult<string>.NotFound("EVENT_NOT_FOUND");

        await redis.RevokeAsync(
            session.EventId,
            session.UserId,
            session.SessionId,
            session.QueueToken,
            CycleKey(config),
            CancellationToken.None);
        session.Status = "REVOKED";
        session.UpdatedAt = QueueTimeZone.Now;
        await repository.SaveChangesAsync();
        return QueueAdminResult<string>.Success("Queue token revoked successfully.");
    }

    public async Task<QueueAdminResult<string>> UnrevokeSessionAsync(string sessionId)
    {
        var session = await repository.GetSessionByIdAsync(sessionId);
        if (session == null)
            return QueueAdminResult<string>.NotFound();
        if (session.Status != "REVOKED")
            return QueueAdminResult<string>.Conflict("SESSION_NOT_REVOKED");

        session.Status = "LEFT";
        session.Position = null;
        session.UpdatedAt = QueueTimeZone.Now;
        await repository.SaveChangesAsync();
        return QueueAdminResult<string>.Success("Queue session restored. The user can register again.");
    }

    public async Task<IReadOnlyList<AbuseAlertResponse>> GetAlertsAsync(string? severity, string? status)
    {
        var alerts = await repository.GetAdminAlertsAsync(
            SupportedAlertTypes,
            SupportedSeverities,
            string.IsNullOrWhiteSpace(severity) ? null : severity.ToUpperInvariant(),
            string.IsNullOrWhiteSpace(status) ? null : status.ToUpperInvariant());
        return alerts.Select(ToResponse).ToArray();
    }

    public async Task<AbuseAlertResponse?> GetAlertAsync(long alertId)
    {
        var alert = await repository.GetAdminAlertByIdAsync(
            alertId,
            SupportedAlertTypes,
            SupportedSeverities);
        return alert == null ? null : ToResponse(alert);
    }

    public async Task<QueueAdminResult<QueueAdminSessionActionResponse>> UnblockSessionAsync(
        string sessionId,
        long? alertId,
        QueueAdminAuditContext auditContext)
    {
        var session = await repository.GetSessionByIdAsync(sessionId);
        if (session == null)
            return QueueAdminResult<QueueAdminSessionActionResponse>.NotFound();

        var alert = await FindRelatedAlertAsync(sessionId, alertId);
        if (alertId.HasValue && alert == null)
            return QueueAdminResult<QueueAdminSessionActionResponse>.NotFound();

        var oldSessionStatus = session.Status;
        var oldAlertStatus = alert?.Status;
        session.Status = "LEFT";
        session.UpdatedAt = QueueTimeZone.Now;
        if (alert != null)
            alert.Status = "OPEN";
        await repository.SaveChangesAsync();
        await WriteAuditLogAsync(
            auditContext,
            "QUEUE_ABUSE_ALERT_UNBLOCKED",
            alert,
            session,
            new { alertStatus = oldAlertStatus, sessionStatus = oldSessionStatus },
            new { alertStatus = alert?.Status, sessionStatus = session.Status });

        return QueueAdminResult<QueueAdminSessionActionResponse>.Success(
            new QueueAdminSessionActionResponse("OPEN", session.Status));
    }

    public async Task<QueueAdminResult<QueueAdminSessionActionResponse>> RecordSessionActionAsync(
        string sessionId,
        string status,
        long? alertId,
        QueueAdminAuditContext auditContext)
    {
        var session = await repository.GetSessionByIdAsync(sessionId);
        if (session == null)
            return QueueAdminResult<QueueAdminSessionActionResponse>.NotFound();

        var alert = await FindRelatedAlertAsync(sessionId, alertId);
        if (alertId.HasValue && alert == null)
            return QueueAdminResult<QueueAdminSessionActionResponse>.NotFound();

        var oldSessionStatus = session.Status;
        var oldAlertStatus = alert?.Status;
        if (status == "BLOCKED")
        {
            var config = await repository.GetConfigurationByEventIdAsync(session.EventId);
            if (config == null)
                return QueueAdminResult<QueueAdminSessionActionResponse>.NotFound("EVENT_NOT_FOUND");

            await redis.RevokeAsync(
                session.EventId,
                session.UserId,
                session.SessionId,
                session.QueueToken,
                CycleKey(config),
                CancellationToken.None);
            session.Status = "BLOCKED";
        }

        session.UpdatedAt = QueueTimeZone.Now;
        if (alert != null)
            alert.Status = status;
        await repository.SaveChangesAsync();
        await WriteAuditLogAsync(
            auditContext,
            status == "BLOCKED" ? "QUEUE_ABUSE_ALERT_BLOCKED" : "QUEUE_ABUSE_ALERT_FLAGGED",
            alert,
            session,
            new { alertStatus = oldAlertStatus, sessionStatus = oldSessionStatus },
            new { alertStatus = alert?.Status, sessionStatus = session.Status });

        return QueueAdminResult<QueueAdminSessionActionResponse>.Success(
            new QueueAdminSessionActionResponse(status, session.Status));
    }

    private Task<AbuseAlert?> FindRelatedAlertAsync(string sessionId, long? alertId) =>
        repository.GetRelatedAlertAsync(
            sessionId,
            alertId,
            SupportedAlertTypes,
            SupportedSeverities);

    private Task WriteAuditLogAsync(
        QueueAdminAuditContext auditContext,
        string action,
        AbuseAlert? alert,
        QueueSession session,
        object oldValue,
        object newValue) =>
        auditLog.LogAsync(
            auditContext.UserId,
            action,
            alert == null ? "QueueSession" : "QueueAbuseAlert",
            alert?.AbuseAlertId.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? session.SessionId,
            oldValue,
            newValue,
            auditContext.IpAddress,
            auditContext.RequestId,
            auditContext.UserAgent);

    private static string CycleKey(QueueConfiguration config) =>
        config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

    private static AbuseAlertResponse ToResponse(AbuseAlert alert)
    {
        var tokenFingerprint = alert.TokenFingerprint.StartsWith("sha256:", StringComparison.Ordinal) ||
            alert.TokenFingerprint.StartsWith("md5:", StringComparison.Ordinal)
            ? alert.TokenFingerprint
            : QueueAbuseDetection.TokenFingerprint(alert.TokenFingerprint);
        return new(
            alert.AbuseAlertId,
            alert.EventId,
            alert.UserId,
            alert.SessionId,
            tokenFingerprint,
            alert.AlertType,
            alert.Severity,
            alert.Status,
            alert.DetectedBehavior,
            QueueTimeZone.AsVietnamOffset(alert.CreatedAt));
    }
}
