using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.Models
{
    [Table("kyc_settings")]
    public class KycSetting
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Column("id")]
        public int Id { get; set; } = 1;

        /// <summary>0 = never auto-delete.</summary>
        [Column("retention_days")]
        public int RetentionDays { get; set; } = 30;

        [Column("keep_document_hash_after_deletion")]
        public bool KeepDocumentHashAfterDeletion { get; set; } = true;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }
    }

}
