using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Models;

[Table("risk_decision")]
[Index("FraudAlertId", Name = "ix_risk_decision_alert_id")]
[Index("CreatedAt", Name = "ix_risk_decision_created_at", AllDescending = true)]
[Index("DeviceFp", Name = "ix_risk_decision_device_fp")]
[Index("IpAddress", Name = "ix_risk_decision_ip")]
[Index("UserId", Name = "ix_risk_decision_user_id")]
[Index("DecisionCode", Name = "risk_decision_code_key", IsUnique = true)]
public partial class RiskDecision
{
    [Key]
    [Column("risk_decision_id")]
    public int RiskDecisionId { get; set; }

    [Column("decision_code")]
    [StringLength(30)]
    public string DecisionCode { get; set; } = null!;

    [Column("fraud_alert_id")]
    public int? FraudAlertId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("order_id")]
    public int? OrderId { get; set; }

    [Column("ticket_id")]
    public int? TicketId { get; set; }

    [Column("session_id")]
    [StringLength(100)]
    public string? SessionId { get; set; }

    [Column("device_fp")]
    [StringLength(128)]
    public string? DeviceFp { get; set; }

    [Column("ip_address")]
    public IPAddress? IpAddress { get; set; }

    [Column("bot_score")]
    [Precision(5, 2)]
    public decimal BotScore { get; set; }

    [Column("fraud_score")]
    [Precision(5, 2)]
    public decimal FraudScore { get; set; }

    [Column("risk_level")]
    [StringLength(10)]
    public string RiskLevel { get; set; } = null!;

    [Column("dominant_type")]
    [StringLength(10)]
    public string? DominantType { get; set; }

    [Column("score_by_group", TypeName = "jsonb")]
    public string? ScoreByGroup { get; set; }

    [Column("triggered_rules", TypeName = "jsonb")]
    public string? TriggeredRules { get; set; }

    [Column("trust_modifiers", TypeName = "jsonb")]
    public string? TrustModifiers { get; set; }

    [Column("hard_rule")]
    [StringLength(20)]
    public string? HardRule { get; set; }

    [Column("signals_missing", TypeName = "jsonb")]
    public string? SignalsMissing { get; set; }

    [Column("rule_version")]
    [StringLength(20)]
    public string RuleVersion { get; set; } = null!;

    [Column("action")]
    [StringLength(20)]
    public string Action { get; set; } = null!;

    [Column("scope")]
    [StringLength(20)]
    public string? Scope { get; set; }

    [Column("reason_public")]
    [StringLength(300)]
    public string? ReasonPublic { get; set; }

    [Column("escalation_level")]
    public short EscalationLevel { get; set; }

    [Column("is_shadow")]
    public bool IsShadow { get; set; }

    [Column("status")]
    [StringLength(15)]
    public string Status { get; set; } = null!;

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("decided_by_type")]
    [StringLength(10)]
    public string DecidedByType { get; set; } = null!;

    [Column("decided_by")]
    public int? DecidedBy { get; set; }

    [Column("manual_reason")]
    [StringLength(500)]
    public string? ManualReason { get; set; }

    [Column("restored_by")]
    public int? RestoredBy { get; set; }

    [Column("restored_at")]
    public DateTime? RestoredAt { get; set; }

    [Column("restore_reason")]
    [StringLength(500)]
    public string? RestoreReason { get; set; }

    [Column("ai_summary", TypeName = "jsonb")]
    public string? AiSummary { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("reason_code")]
    [StringLength(60)]
    public string? ReasonCode { get; set; }

    [Column("notified_at")]
    public DateTime? NotifiedAt { get; set; }

    [ForeignKey("FraudAlertId")]
    [InverseProperty("RiskDecisions")]
    public virtual FraudAlert? FraudAlert { get; set; }

    [InverseProperty("RiskDecision")]
    public virtual RiskAppeal? RiskAppeal { get; set; }

    [InverseProperty("RiskDecision")]
    public virtual ICollection<RiskNotification> RiskNotifications { get; set; } = new List<RiskNotification>();
}
