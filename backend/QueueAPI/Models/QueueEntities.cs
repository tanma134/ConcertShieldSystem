namespace QueueAPI.Models;

using QueueAPI.Time;

public sealed class QueueConfiguration
{
    public int QueueConfigurationId { get; set; }
    public int EventId { get; set; }
    public DateTime PreQueueStartTime { get; set; }
    public DateTime PreQueueEndTime { get; set; }
    public int AdmissionRate { get; set; }
    public bool QueueEnabled { get; set; } = true;
    public string FairnessPolicy { get; set; } = "RANDOMIZED";
    public DateTime CreatedAt { get; set; } = QueueTimeZone.Now;
    public DateTime UpdatedAt { get; set; } = QueueTimeZone.Now;
}

public sealed class QueueSession
{
    public long QueueSessionId { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string QueueToken { get; set; } = string.Empty;
    public string Status { get; set; } = "REGISTERED";
    public int? Position { get; set; }
    public DateTime CreatedAt { get; set; } = QueueTimeZone.Now;
    public DateTime UpdatedAt { get; set; } = QueueTimeZone.Now;
}

public sealed class AbuseAlert
{
    public long AbuseAlertId { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string QueueToken { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "LOW";
    public string Status { get; set; } = "OPEN";
    public string DetectedBehavior { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = QueueTimeZone.Now;
}
