using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.Models
{
    [Table("kyc_deletion_requests")]
    public class KycDeletionRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        /// <summary>Pending | Completed | Rejected</summary>
        [Column("status")]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        [Column("requested_at")]
        public DateTime RequestedAt { get; set; }

        [Column("processed_at")]
        public DateTime? ProcessedAt { get; set; }

        [Column("processed_by")]
        public int? ProcessedBy { get; set; }

        /// <summary>Reason when rejected.</summary>
        [Column("note")]
        public string? Note { get; set; }
    }
}