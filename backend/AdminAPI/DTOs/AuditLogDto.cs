
namespace AdminAPI.DTOs
{
    public class AuditLogDto
    {
        public long AuditLogId { get; set; }
        public int? UserId { get; set; }
        public string ActorType { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public string? EntityId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? IpAddress { get; set; }
        public string? RequestId { get; set; }
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class CreateAuditLogRequest
    {
        public int? UserId { get; set; }
        public string ActorType { get; set; } = "user";
        public string Action { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public string? EntityId { get; set; }
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public string? IpAddress { get; set; }
        public string? RequestId { get; set; }
        public string? UserAgent { get; set; }
    }
}