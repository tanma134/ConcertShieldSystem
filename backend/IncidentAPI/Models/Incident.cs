using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncidentAPI.Models;

// Entity map chính xác với bảng public.incidents trong incident_db
[Table("incidents")]
public class Incident
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("event_id")]
    [Required]
    public int EventId { get; set; }

    [Column("event_name")]
    [StringLength(255)]
    public string? EventName { get; set; }

    [Column("reporter_id")]
    [Required]
    public int ReporterId { get; set; }

    [Column("reporter_name")]
    [StringLength(100)]
    [Required]
    public string ReporterName { get; set; } = "Staff";

    [Column("title")]
    [StringLength(200)]
    [Required]
    public string Title { get; set; } = null!;

    [Column("category")]
    [StringLength(50)]
    [Required]
    public string Category { get; set; } = "Security";

    [Column("severity")]
    [StringLength(20)]
    [Required]
    public string Severity { get; set; } = "Medium";

    [Column("location")]
    [StringLength(255)]
    [Required]
    public string Location { get; set; } = null!;

    [Column("description")]
    [Required]
    public string Description { get; set; } = null!;

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = "Reported";

    [Column("is_escalated")]
    public bool IsEscalated { get; set; } = false;

    [Column("escalated_to_admin_id")]
    public int? EscalatedToAdminId { get; set; }

    [Column("escalated_at")]
    public DateTime? EscalatedAt { get; set; }

    [Column("escalation_reason")]
    public string? EscalationReason { get; set; }

    [Column("resolution_summary")]
    public string? ResolutionSummary { get; set; }

    [Column("action_taken")]
    public string? ActionTaken { get; set; }

    [Column("resolved_by_id")]
    public int? ResolvedById { get; set; }

    [Column("resolved_by_name")]
    [StringLength(100)]
    public string? ResolvedByName { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<IncidentEvidence> Evidences { get; set; } = new List<IncidentEvidence>();
    public virtual ICollection<IncidentUpdate> Updates { get; set; } = new List<IncidentUpdate>();
}
