using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Models;

[Table("risk_appeal")]
[Index("UserId", Name = "ix_risk_appeal_user_id")]
[Index("RiskDecisionId", Name = "uq_risk_appeal_decision", IsUnique = true)]
public partial class RiskAppeal
{
    [Key]
    [Column("risk_appeal_id")]
    public int RiskAppealId { get; set; }

    [Column("risk_decision_id")]
    public int RiskDecisionId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("reason")]
    [StringLength(2000)]
    public string Reason { get; set; } = null!;

    [Column("evidence", TypeName = "jsonb")]
    public string? Evidence { get; set; }

    [Column("status")]
    [StringLength(15)]
    public string Status { get; set; } = null!;

    [Column("sla_due_at")]
    public DateTime SlaDueAt { get; set; }

    [Column("reviewed_by")]
    public int? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("review_note")]
    [StringLength(1000)]
    public string? ReviewNote { get; set; }

    [Column("ai_summary", TypeName = "jsonb")]
    public string? AiSummary { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("RiskDecisionId")]
    [InverseProperty("RiskAppeal")]
    public virtual RiskDecision RiskDecision { get; set; } = null!;
}
