using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthenticationAPI.Models;

[Table("kyc_consent_versions")]
public class KycConsentVersion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("version")]
    [StringLength(20)]
    public string Version { get; set; } = null!;

    [Column("title")]
    [StringLength(500)]
    public string Title { get; set; } = null!;

    /// <summary>JSON array of { Heading, Content }.</summary>
    [Column("content_json")]
    public string ContentJson { get; set; } = null!;

    [Column("checkbox_text")]
    public string CheckboxText { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }
}