using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Models;

[Table("risk_notification")]
[Index("UserId", "CreatedAt", Name = "ix_risk_notification_user", IsDescending = new[] { false, true })]
public partial class RiskNotification
{
    [Key]
    [Column("risk_notification_id")]
    public int RiskNotificationId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("risk_decision_id")]
    public int? RiskDecisionId { get; set; }

    [Column("risk_appeal_id")]
    public int? RiskAppealId { get; set; }

    [Column("type")]
    [StringLength(30)]
    public string Type { get; set; } = null!;

    [Column("scope")]
    [StringLength(20)]
    public string? Scope { get; set; }

    [Column("title")]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Column("message")]
    [StringLength(1000)]
    public string Message { get; set; } = null!;

    [Column("can_appeal")]
    public bool CanAppeal { get; set; }

    [Column("appeal_deadline_at")]
    public DateTime? AppealDeadlineAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [ForeignKey("RiskDecisionId")]
    [InverseProperty("RiskNotifications")]
    public virtual RiskDecision? RiskDecision { get; set; }
}
