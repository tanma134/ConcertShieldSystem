using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncidentAPI.Models;

// Entity map chính xác với bảng public.incident_updates trong incident_db (UC_16.3)
[Table("incident_updates")]
public class IncidentUpdate
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("incident_id")]
    [Required]
    public int IncidentId { get; set; }

    [Column("updated_by_id")]
    [Required]
    public int UpdatedById { get; set; }

    [Column("updated_by_name")]
    [StringLength(100)]
    [Required]
    public string UpdatedByName { get; set; } = "Staff";

    [Column("action_type")]
    [StringLength(50)]
    [Required]
    public string ActionType { get; set; } = "Update";

    [Column("note")]
    [Required]
    public string Note { get; set; } = null!;

    [Column("previous_status")]
    [StringLength(30)]
    public string? PreviousStatus { get; set; }

    [Column("new_status")]
    [StringLength(30)]
    public string? NewStatus { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("IncidentId")]
    public virtual Incident Incident { get; set; } = null!;
}
