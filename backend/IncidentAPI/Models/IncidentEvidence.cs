using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncidentAPI.Models;

// Entity map chính xác với bảng public.incident_evidences trong incident_db (UC_16.2)
[Table("incident_evidences")]
public class IncidentEvidence
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("incident_id")]
    [Required]
    public int IncidentId { get; set; }

    [Column("file_url")]
    [StringLength(1000)]
    [Required]
    public string FileUrl { get; set; } = null!;

    [Column("public_id")]
    [StringLength(255)]
    [Required]
    public string PublicId { get; set; } = null!;

    [Column("file_name")]
    [StringLength(255)]
    [Required]
    public string FileName { get; set; } = null!;

    [Column("file_type")]
    [StringLength(50)]
    [Required]
    public string FileType { get; set; } = "image";

    [Column("file_size")]
    public long FileSize { get; set; }

    [Column("mime_type")]
    [StringLength(100)]
    [Required]
    public string MimeType { get; set; } = null!;

    [Column("caption")]
    [StringLength(500)]
    public string? Caption { get; set; }

    [Column("uploaded_by_id")]
    [Required]
    public int UploadedById { get; set; }

    [Column("uploaded_by_name")]
    [StringLength(100)]
    [Required]
    public string UploadedByName { get; set; } = "Staff";

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("IncidentId")]
    public virtual Incident Incident { get; set; } = null!;
}
