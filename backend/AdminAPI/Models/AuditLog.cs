using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Models;

[Table("audit_logs")]
[Index("EntityType", "EntityId", Name = "ix_audit_logs_entity")]
[Index("UserId", Name = "ix_audit_logs_user_id")]
public partial class AuditLog
{
    [Key]
    [Column("audit_log_id")]
    public long AuditLogId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("actor_type")]
    [StringLength(20)]
    public string ActorType { get; set; } = null!;

    [Column("action")]
    [StringLength(100)]
    public string Action { get; set; } = null!;

    [Column("entity_type")]
    [StringLength(50)]
    public string EntityType { get; set; } = null!;

    [Column("entity_id")]
    [StringLength(50)]
    public string? EntityId { get; set; }

    [Column("old_value", TypeName = "jsonb")]
    public string? OldValue { get; set; }

    [Column("new_value", TypeName = "jsonb")]
    public string? NewValue { get; set; }

    [Column("ip_address")]
    public IPAddress? IpAddress { get; set; }

    [Column("request_id")]
    [StringLength(100)]
    public string? RequestId { get; set; }

    [Column("user_agent")]
    [StringLength(500)]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
