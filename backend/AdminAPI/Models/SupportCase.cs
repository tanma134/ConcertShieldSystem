using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminAPI.Models;

// Customer support case and dispute entity
[Table("support_cases")]
public class SupportCase
{
    [Key]
    [Column("case_id")]
    public int CaseId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("case_number")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("customer_id")]
    public int? CustomerId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("customer_email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("customer_phone")]
    public string? CustomerPhone { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("category")]
    public string Category { get; set; } = "General Inquiry";

    [Required]
    [MaxLength(20)]
    [Column("priority")]
    public string Priority { get; set; } = "Medium";

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Open";

    // Lien ket giao dich don hang / ve theo quy tac BR-313
    [Column("order_id")]
    public int? OrderId { get; set; }

    [Column("ticket_id")]
    public int? TicketId { get; set; }

    [MaxLength(100)]
    [Column("payment_reference")]
    public string? PaymentReference { get; set; }

    // Staff assignment information
    [Column("assigned_staff_id")]
    public int? AssignedStaffId { get; set; }

    [MaxLength(150)]
    [Column("assigned_staff_name")]
    public string? AssignedStaffName { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("assigned_by")]
    public int? AssignedBy { get; set; }

    // Case closure resolution details
    [Column("resolution")]
    public string? Resolution { get; set; }

    [Column("resolved_by")]
    public int? ResolvedBy { get; set; }

    [MaxLength(150)]
    [Column("resolved_by_name")]
    public string? ResolvedByName { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("created_by")]
    public int CreatedBy { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("created_by_name")]
    public string CreatedByName { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<SupportCaseNote> Notes { get; set; } = new();
}
