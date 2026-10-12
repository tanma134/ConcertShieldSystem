namespace EventAPI.Models;

// Hồ sơ hiện tại nằm trên Event; các bảng này giữ tài liệu và quyết định lịch sử.
public class ComplianceDocument
{
    public long Id { get; set; }
    public int EventId { get; set; }
    public int Version { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string PublicId { get; set; } = "";
    public string SecureUrl { get; set; } = "";
    public string ResourceType { get; set; } = "raw";
    public long Size { get; set; }
    public int SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
}
public class ComplianceReview
{
    public long Id { get; set; }
    public int EventId { get; set; }
    public int Version { get; set; }
    public string Decision { get; set; } = "";
    public string Notes { get; set; } = "";
    public int ReviewedBy { get; set; }
    public DateTime ReviewedAt { get; set; }
}
public class EventChangeRequest
{
    public long Id { get; set; }
    public int EventId { get; set; }
    public string Type { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string Reason { get; set; } = "";
    public DateTime OldStartsAt { get; set; }
    public DateTime OldEndsAt { get; set; }
    public DateTime? NewStartsAt { get; set; }
    public DateTime? NewEndsAt { get; set; }
    public string OldStatus { get; set; } = "";
    public int EventVersion { get; set; }
    public int SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public string ProcessingStatus { get; set; } = "NotApplied";
}
// Outbox cùng EventDB: quyết định đã commit vẫn được gửi lại nếu service đích tạm ngừng.
public class GovernanceOutbox
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
