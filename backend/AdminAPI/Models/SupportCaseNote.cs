using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminAPI.Models;

// Nhat ky xu ly, trao doi va lich su trang thai ho so
[Table("support_case_notes")]
public class SupportCaseNote
{
    [Key]
    [Column("note_id")]
    public int NoteId { get; set; }

    [Column("case_id")]
    public int CaseId { get; set; }

    [ForeignKey(nameof(CaseId))]
    public SupportCase? SupportCase { get; set; }

    [Column("author_id")]
    public int AuthorId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("author_name")]
    public string AuthorName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("author_role")]
    public string AuthorRole { get; set; } = string.Empty;

    // Phan loai ghi chu: InternalNote, Assignment, StatusChange, Resolution
    [Required]
    [MaxLength(50)]
    [Column("note_type")]
    public string NoteType { get; set; } = "InternalNote";

    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
