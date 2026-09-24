using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.Models
{
    [Table("kyc_access_logs")]
    public class KycAccessLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        /// <summary>Whose data was accessed.</summary>
        [Column("subject_user_id")]
        public int SubjectUserId { get; set; }

        [Column("ekyc_id")]
        public int? EkycId { get; set; }

        /// <summary>Who accessed it (null for system jobs).</summary>
        [Column("actor_user_id")]
        public int? ActorUserId { get; set; }

        /// <summary>User | Admin | System</summary>
        [Column("actor_type")]
        [StringLength(20)]
        public string ActorType { get; set; } = "User";

     
        [Column("action")]
        [StringLength(50)]
        public string Action { get; set; } = null!;

        [Column("ip_address")]
        [StringLength(45)]
        public string? IpAddress { get; set; }

        [Column("details")]
        public string? Details { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
