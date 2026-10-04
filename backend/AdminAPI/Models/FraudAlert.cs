using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Models;

[Table("fraud_alerts")]
[Index("CreatedAt", Name = "ix_fraud_alerts_created_at", AllDescending = true)]
[Index("EventId", Name = "ix_fraud_alerts_event_id")]
[Index("RiskLevel", "Status", Name = "ix_fraud_alerts_level_status")]
[Index("OrderId", Name = "ix_fraud_alerts_order_id")]
[Index("TicketId", Name = "ix_fraud_alerts_ticket_id")]
[Index("UserId", Name = "ix_fraud_alerts_user_id")]
public partial class FraudAlert
{
    [Key]
    [Column("fraud_alert_id")]
    public int FraudAlertId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("order_id")]
    public int? OrderId { get; set; }

    [Column("ticket_id")]
    public int? TicketId { get; set; }

    [Column("event_id")]
    public int? EventId { get; set; }

    [Column("alert_type")]
    [StringLength(30)]
    public string AlertType { get; set; } = null!;

    [Column("risk_score")]
    [Precision(5, 2)]
    public decimal RiskScore { get; set; }

    [Column("details")]
    public string? Details { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("reviewed_by")]
    public int? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("bot_score")]
    [Precision(5, 2)]
    public decimal BotScore { get; set; }

    [Column("fraud_score")]
    [Precision(5, 2)]
    public decimal FraudScore { get; set; }

    [Column("risk_level")]
    [StringLength(10)]
    public string RiskLevel { get; set; } = null!;

    [Column("session_id")]
    [StringLength(100)]
    public string? SessionId { get; set; }

    [Column("ai_summary", TypeName = "jsonb")]
    public string? AiSummary { get; set; }

    [Column("ai_summary_at")]
    public DateTime? AiSummaryAt { get; set; }

    [InverseProperty("FraudAlert")]
    public virtual ICollection<RiskDecision> RiskDecisions { get; set; } = new List<RiskDecision>();
}
