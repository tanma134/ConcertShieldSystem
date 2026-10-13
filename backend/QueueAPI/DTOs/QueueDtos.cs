namespace QueueAPI.DTOs;

public sealed record ConfigureQueueRequest(DateTime PreQueueStartTime, DateTime PreQueueEndTime, int AdmissionRate, bool QueueEnabled = true, string FairnessPolicy = "RANDOMIZED");
public sealed record QueueStatusResponse(int EventId, string? QueueToken, string Status, int? Position, int TotalWaiting, int? EstimatedWaitTimeMinutes, DateTimeOffset? AdmissionExpiresAt, DateTimeOffset LastUpdated, string? ErrorCode = null);
public sealed record ConfigureQueueResponse(int EventId, DateTimeOffset PreQueueStartTime, DateTimeOffset PreQueueEndTime, int AdmissionRate, bool QueueEnabled, string FairnessPolicy);
public sealed record RevokeQueueRequest(string? Reason);
public sealed record SessionActionRequest(string? Reason);
public sealed record QueueAdminSessionResponse(long Id, int EventId, int UserId, string SessionId, string Status, int? Position, string TokenPreview, DateTimeOffset UpdatedAt);
public sealed record AbuseAlertResponse(long AlertId, int EventId, int UserId, string SessionId, string QueueToken, string AlertType, string Severity, string Status, string DetectedBehavior, DateTimeOffset CreatedAt);
