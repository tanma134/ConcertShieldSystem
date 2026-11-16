using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Npgsql;
using QueueAPI.Data;
using QueueAPI.DTOs;
using QueueAPI.Models;
using QueueAPI.Time;

namespace QueueAPI.Services;

public sealed class QueueService(QueueDbContext db, IRedisQueueStore redis, IHttpClientFactory httpClientFactory, ILogger<QueueService> logger) : IQueueService
{
    public async Task<ConfigureQueueResponse> ConfigureAsync(int eventId, ConfigureQueueRequest request, int userId)
    {
        await ValidateEventAsync(eventId);
        // A value without an offset is a Vietnam local wall-clock value.
        // Values received with Z/UTC are preserved as UTC.
        var start = QueueTimeZone.ToVietnam(request.PreQueueStartTime);
        var end = QueueTimeZone.ToVietnam(request.PreQueueEndTime);
        if (start >= end) throw new ArgumentException("Pre-queue start time must be before end time.");
        if (request.AdmissionRate <= 0) throw new ArgumentException("Admission rate must be greater than zero.");
        var config = await db.QueueConfigurations.SingleOrDefaultAsync(x => x.EventId == eventId);
        if (config == null)
        {
            config = new QueueConfiguration { EventId = eventId };
            db.QueueConfigurations.Add(config);
        }
        config.PreQueueStartTime = start;
        config.PreQueueEndTime = end;
        config.AdmissionRate = request.AdmissionRate;
        config.QueueEnabled = request.QueueEnabled;
        config.FairnessPolicy = request.FairnessPolicy.Trim().ToUpperInvariant();
        config.UpdatedAt = QueueTimeZone.Now;
        await db.SaveChangesAsync();
        logger.LogInformation("Queue configured eventId={EventId} userId={UserId}", eventId, userId);
        return new(eventId, QueueTimeZone.AsVietnamOffset(start), QueueTimeZone.AsVietnamOffset(end), config.AdmissionRate, config.QueueEnabled, config.FairnessPolicy);
    }

    public async Task<QueueStatusResponse> RegisterAsync(int eventId, int userId)
    {
        var config = await GetConfigAsync(eventId);
        var cycleKey = CycleKey(config);
        var now = QueueTimeZone.Now;
        if (!config.QueueEnabled) throw new InvalidOperationException("QUEUE_NOT_AVAILABLE");
        if (now < config.PreQueueStartTime) throw new InvalidOperationException("PREQUEUE_NOT_OPEN");
        if (now > config.PreQueueEndTime) throw new InvalidOperationException("PREQUEUE_CLOSED");
        var requestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var registrationTimestamps = await redis.RecordRegistrationRequestAsync(
            eventId, userId, requestTimestamp, CancellationToken.None);
        var existing = await db.QueueSessions.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);
        if (existing != null && existing.Status is not ("LEFT" or "EXPIRED"))
        {
            await DetectAutomatedRequestPatternAsync(existing, registrationTimestamps, now);
            if (existing.Status is "REVOKED" or "BLOCKED")
                throw new InvalidOperationException("ALREADY_REGISTERED");

            var repeatedTimestamps = await redis.RecordRepeatedRegistrationAttemptAsync(
                eventId, userId, requestTimestamp, CancellationToken.None);
            await DetectMultipleRegistrationAttemptsAsync(existing, repeatedTimestamps, now);
            if (await redis.RegisterAsync(eventId, userId, existing.QueueToken, cycleKey, CancellationToken.None))
                throw new InvalidOperationException("ALREADY_REGISTERED");
            throw new InvalidOperationException("QUEUE_REGISTRATION_UNAVAILABLE");
        }
        if (existing == null || existing.Status is "LEFT" or "EXPIRED")
            await redis.ResetUserAsync(eventId, userId, cycleKey, CancellationToken.None);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        if (existing == null)
        {
            existing = new QueueSession { EventId = eventId, UserId = userId };
            db.QueueSessions.Add(existing);
        }

        existing.SessionId = Guid.NewGuid().ToString("N");
        existing.QueueToken = token;
        existing.Status = "REGISTERED";
        existing.Position = null;
        existing.CreatedAt = QueueTimeZone.Now;
        existing.UpdatedAt = existing.CreatedAt;
        await db.SaveChangesAsync();

        await DetectAutomatedRequestPatternAsync(existing, registrationTimestamps, now);
        if (!await redis.RegisterAsync(eventId, userId, token, cycleKey, CancellationToken.None))
            throw new InvalidOperationException("ALREADY_REGISTERED");

        return ToResponse(eventId, token, "REGISTERED", null, 0, null, config);
    }

    public async Task<QueueStatusResponse> JoinAsync(int eventId, int userId, string? token)
    {
        var config = await GetConfigAsync(eventId);
        var session = await GetSessionAsync(eventId, userId, token);
        if (QueueTimeZone.Now < config.PreQueueEndTime)
        {
            var preQueueResult = await redis.JoinAsync(eventId, userId, session.QueueToken, CancellationToken.None);
            if (preQueueResult.Status == "TOKEN_INVALID")
                throw new UnauthorizedAccessException("TOKEN_INVALID");

            return ToResponse(eventId, session.QueueToken, "PREQUEUE", null, preQueueResult.Total, null, config);
        }

        await redis.FinalizeRandomizationAsync(eventId, CycleKey(config), CancellationToken.None);
        if (config.QueueEnabled)
            await redis.AdmitNextAsync(eventId, config.AdmissionRate, CancellationToken.None);

        var result = await redis.JoinAsync(eventId, userId, session.QueueToken, CancellationToken.None);
        if (result.Status == "TOKEN_INVALID")
            throw new UnauthorizedAccessException("TOKEN_INVALID");

        session.Status = result.Status;
        session.Position = result.Position == 0 ? null : result.Position;
        session.UpdatedAt = QueueTimeZone.Now;
        await db.SaveChangesAsync();
        return ToResponse(eventId, session.QueueToken, result.Status, result.Position == 0 ? null : result.Position, result.Total, null, config);
    }

    public async Task<QueueStatusResponse> GetStatusAsync(int eventId, int userId, string? token)
    {
        var session = await GetSessionAsync(eventId, userId, token);
        var config = await GetConfigAsync(eventId);
        if (QueueTimeZone.Now >= config.PreQueueEndTime)
        {
            await redis.FinalizeRandomizationAsync(eventId, CycleKey(config), CancellationToken.None);
            if (config.QueueEnabled)
                await redis.AdmitNextAsync(eventId, config.AdmissionRate, CancellationToken.None);
        }

        var result = await redis.GetStatusAsync(eventId, userId, session.QueueToken, CancellationToken.None);
        if (result.Status == "TOKEN_INVALID")
            throw new UnauthorizedAccessException("TOKEN_INVALID");

        var status = result.Status;
        if (session.Status == "ADMITTED" && status != "ADMITTED")
            status = "EXPIRED";
        session.Status = status == "PREQUEUE" ? "REGISTERED" : status;
        session.Position = status == "EXPIRED" ? null : result.Position == 0 ? null : result.Position;
        session.UpdatedAt = QueueTimeZone.Now;
        await db.SaveChangesAsync();
        return ToResponse(eventId, session.QueueToken, status, session.Position, result.Total, result.AdmissionExpiresAt, config);
    }

    public async Task LeaveAsync(int eventId, int userId, string? token)
    {
        var session = await GetSessionAsync(eventId, userId, token);
        var config = await GetConfigAsync(eventId);
        await redis.LeaveAsync(eventId, userId, session.QueueToken, CycleKey(config), CancellationToken.None);
        session.Status = "LEFT";
        session.UpdatedAt = QueueTimeZone.Now;
        await db.SaveChangesAsync();
    }

    private async Task<QueueConfiguration> GetConfigAsync(int eventId) => await db.QueueConfigurations.SingleOrDefaultAsync(x => x.EventId == eventId)
        ?? throw new KeyNotFoundException("EVENT_NOT_FOUND");

    private async Task<QueueSession> GetSessionAsync(int eventId, int userId, string? token)
    {
        var session = await db.QueueSessions.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);
        if (session == null || (token != null && !string.Equals(token, session.QueueToken, StringComparison.Ordinal)))
        {
            await DetectRevokedTokenReplayAsync(eventId, token);
            throw new UnauthorizedAccessException("TOKEN_INVALID");
        }
        if (session.Status is "REVOKED" or "BLOCKED")
            await DetectRevokedTokenReplayAsync(eventId, token);
        if (session.Status is "LEFT" or "REVOKED" or "BLOCKED") throw new InvalidOperationException("TOKEN_REVOKED");
        return session;
    }

    private async Task DetectMultipleRegistrationAttemptsAsync(
        QueueSession session,
        IReadOnlyList<long> timestamps,
        DateTime detectedAt)
    {
        if (timestamps.Count < QueueAbuseDetection.MultipleRegistrationAttemptsThreshold)
            return;

        await CreateAutomaticAlertAsync(
            session.EventId,
            session.UserId,
            session.SessionId,
            QueueAbuseDetection.TokenFingerprint(session.QueueToken),
            "MULTIPLE_REGISTRATION_ATTEMPTS",
            "LOW",
            new
            {
                attemptCount = timestamps.Count,
                detectionWindowSeconds = QueueAbuseDetection.MultipleRegistrationWindowSeconds,
                attemptTimestamps = ToVietnamTimestamps(timestamps),
                detectionTime = QueueTimeZone.AsVietnamOffset(detectedAt)
            },
            detectedAt);
    }

    private async Task DetectAutomatedRequestPatternAsync(
        QueueSession session,
        IReadOnlyList<long> timestamps,
        DateTime detectedAt)
    {
        if (!QueueAbuseDetection.HasRegularAutomatedRequestPattern(timestamps, out var coefficientOfVariation))
            return;

        await CreateAutomaticAlertAsync(
            session.EventId,
            session.UserId,
            session.SessionId,
            QueueAbuseDetection.TokenFingerprint(session.QueueToken),
            "AUTOMATED_REQUEST_PATTERN",
            "MEDIUM",
            new
            {
                requestCount = QueueAbuseDetection.AutomatedRequestThreshold,
                detectionWindowSeconds = QueueAbuseDetection.AutomatedRequestWindowSeconds,
                requestTimestamps = ToVietnamTimestamps(
                    timestamps.OrderByDescending(timestamp => timestamp)
                        .Take(QueueAbuseDetection.AutomatedRequestThreshold)
                        .OrderBy(timestamp => timestamp)
                        .ToArray()),
                coefficientOfVariation,
                maximumCoefficientOfVariation = QueueAbuseDetection.AutomatedRequestMaxCoefficientOfVariation,
                detectionTime = QueueTimeZone.AsVietnamOffset(detectedAt)
            },
            detectedAt);
    }

    private async Task DetectRevokedTokenReplayAsync(int eventId, string? token)
    {
        if (string.IsNullOrEmpty(token))
            return;

        var snapshot = await redis.RecordRevokedTokenReuseAsync(
            eventId, token, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), CancellationToken.None);
        if (snapshot == null || snapshot.AttemptTimestamps.Count < QueueAbuseDetection.TokenReplayThreshold)
            return;

        var detectedAt = QueueTimeZone.Now;
        await CreateAutomaticAlertAsync(
            eventId,
            snapshot.UserId,
            snapshot.SessionId,
            snapshot.TokenFingerprint,
            "TOKEN_REPLAY",
            "HIGH",
            new
            {
                invalidTokenReuseAttemptCount = snapshot.AttemptTimestamps.Count,
                detectionWindowSeconds = QueueAbuseDetection.TokenReplayWindowSeconds,
                tokenFingerprint = snapshot.TokenFingerprint,
                attemptTimestamps = ToVietnamTimestamps(snapshot.AttemptTimestamps),
                detectionTime = QueueTimeZone.AsVietnamOffset(detectedAt)
            },
            detectedAt);
    }

    private async Task CreateAutomaticAlertAsync(
        int eventId,
        int userId,
        string sessionId,
        string tokenFingerprint,
        string alertType,
        string severity,
        object evidence,
        DateTime detectedAt)
    {
        if (await db.AbuseAlerts.AnyAsync(x => x.SessionId == sessionId && x.AlertType == alertType))
            return;

        var alert = new AbuseAlert
        {
            EventId = eventId,
            UserId = userId,
            SessionId = sessionId,
            TokenFingerprint = tokenFingerprint,
            AlertType = alertType,
            Severity = severity,
            Status = "OPEN",
            DetectedBehavior = JsonSerializer.Serialize(evidence),
            CreatedAt = detectedAt
        };
        db.AbuseAlerts.Add(alert);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsDuplicateAlertIncident(exception))
        {
            db.Entry(alert).State = EntityState.Detached;
            return;
        }

        logger.LogWarning(
            "Automatic queue abuse alert created type={AlertType} eventId={EventId} userId={UserId} sessionId={SessionId}",
            alertType, eventId, userId, sessionId);
    }

    private static IReadOnlyList<DateTimeOffset> ToVietnamTimestamps(IEnumerable<long> timestamps) =>
        timestamps.Select(timestamp =>
            DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToOffset(TimeSpan.FromHours(7))).ToArray();

    private static bool IsDuplicateAlertIncident(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName == "ux_queue_abuse_alerts_session_type")
                return true;
        }
        return false;
    }

    private static QueueStatusResponse ToResponse(int eventId, string token, string status, int? position, int total, DateTimeOffset? expiresAt, QueueConfiguration config) =>
        new(eventId, token, status, position, total, EstimateWait(position, config.AdmissionRate), expiresAt, QueueTimeZone.NowOffset, QueueTimeZone.AsVietnamOffset(config.PreQueueStartTime), QueueTimeZone.AsVietnamOffset(config.PreQueueEndTime));

    private static int? EstimateWait(int? position, int admissionRate)
    {
        if (!position.HasValue) return null;
        if (admissionRate <= 0) return Math.Max(1, position.Value / 2);
        return Math.Max(1, (int)Math.Ceiling(position.Value / (double)admissionRate));
    }

    private static string CycleKey(QueueConfiguration config) =>
        config.PreQueueEndTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);

    private async Task ValidateEventAsync(int eventId)
    {
        var client = httpClientFactory.CreateClient("EventAPI");
        try
        {
            using var response = await client.GetAsync($"api/events/{eventId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new KeyNotFoundException("EVENT_NOT_FOUND");
            if (!response.IsSuccessStatusCode)
                throw new EventApiUnavailableException();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "EventAPI validation failed for eventId={EventId}", eventId);
            throw new EventApiUnavailableException();
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "EventAPI validation timed out for eventId={EventId}", eventId);
            throw new EventApiUnavailableException();
        }
    }
}

public sealed class EventApiUnavailableException : Exception
{
    public EventApiUnavailableException() : base("EVENT_API_UNAVAILABLE") { }
}
