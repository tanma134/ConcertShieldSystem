using System.ComponentModel.DataAnnotations;

namespace AdminAPI.DTOs
{
    public class FraudCaseQuery
    {
        public string? Status { get; set; }          // Open | InReview | Resolved | FalsePositive | Dismissed
        public string? RiskLevel { get; set; }       // Low | Medium | High | Critical
        public string? AlertType { get; set; }       // Bot | Fraud
        public int? AssignedTo { get; set; }         // analyst userId (fraud_alerts.assigned_to)
        public int? UserId { get; set; }             // user bị cảnh báo
        public bool? Unassigned { get; set; }        // true: chỉ ca chưa có analyst
        public bool? OverdueOnly { get; set; }       // true: ca Open/InReview High|Critical quá SLA
        public string? Keyword { get; set; }         // mã case, orderId, ticketId hoặc userId
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? SortBy { get; set; }          // createdAt (mặc định) | riskScore
        public bool Desc { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class FraudCaseListItemDto
    {
        public int CaseId { get; set; }
        public int? UserId { get; set; }
        public string AlertType { get; set; } = null!;
        public string RiskLevel { get; set; } = null!;
        public decimal RiskScore { get; set; }
        public string Status { get; set; } = null!;
        public int? AssignedTo { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime SlaDueAt { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class AssignFraudCaseRequest
    {
        [Range(1, int.MaxValue)]
        public int AnalystId { get; set; }
        [MaxLength(500)]
        public string? Note { get; set; }
    }

    public class ResolveFraudCaseRequest
    {
        [Required]
        public string Outcome { get; set; } = null!;
        [Required, MaxLength(500)]
        public string Note { get; set; } = null!;
    }

    public class FraudCaseResultDto
    {
        public int CaseId { get; set; }
        public string Status { get; set; } = null!;
        public int? AssignedTo { get; set; }
        public DateTime? AssignedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? PreviousStatus { get; set; }
        public int? PreviousAssignee { get; set; }
    }

    public class RiskPolicyItemDto
    {
        public string Key { get; set; } = null!;
        public string Value { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string? Description { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateRiskPolicyItem
    {
        [Required] public string Key { get; set; } = null!;
        [Required] public string Value { get; set; } = null!;
    }

    public class UpdateRiskPolicyRequest
    {
        [Required, MinLength(1), MaxLength(50)]
        public List<UpdateRiskPolicyItem> Items { get; set; } = new();
        [Required, MaxLength(500)]
        public string Reason { get; set; } = null!;
    }

    public class RiskPolicyChangeDto
    {
        public string Key { get; set; } = null!;
        public string? OldValue { get; set; }
        public string NewValue { get; set; } = null!;
    }

    public class UpdateRiskPolicyResult
    {
        public List<RiskPolicyChangeDto> Changes { get; set; } = new();
        public string Note { get; set; } = "Changes apply to new risk evaluations within about 60 seconds.";
    }
}
