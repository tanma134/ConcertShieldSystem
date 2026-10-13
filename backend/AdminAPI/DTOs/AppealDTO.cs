using System.Text.Json;

namespace AdminAPI.DTOs
{
    public class RiskDecisionPublicDto
    {
        public int RiskDecisionId { get; set; }
        public string DecisionCode { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string? Scope { get; set; }
        public string Status { get; set; } = null!;
        public string ReasonPublic { get; set; } = null!;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool CanAppeal { get; set; }
        public string? CannotAppealReason { get; set; }
        public int? ExistingAppealId { get; set; }
        public string? ExistingAppealStatus { get; set; }
    }


    public class RiskDecisionAdminDto : RiskDecisionDetailDto
    {
        public int? UserId { get; set; }
        public int? OrderId { get; set; }
        public int? TicketId { get; set; }
        public int? FraudAlertId { get; set; }
    }



    public class EvidenceFileDto
    {
        public string FileName { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
    }

    public class SubmitAppealRequest
    {
        public int RiskDecisionId { get; set; }
        public string Reason { get; set; } = null!;
        public List<EvidenceFileDto> Evidence { get; set; } = new();
    }

    public class AppealResultDto
    {
        public int RiskAppealId { get; set; }
        public int RiskDecisionId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime SlaDueAt { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Chỉ có khi Admin resolve (BR-249 audit): các quyết định block đã được gỡ khi chấp nhận kháng nghị.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<int>? LiftedDecisionIds { get; set; }
    }


    public class AppealQuery
    {
        public string? Status { get; set; }
        public bool? OverdueOnly { get; set; }
        public int? UserId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class AppealListItemDto
    {
        public int RiskAppealId { get; set; }
        public int RiskDecisionId { get; set; }
        public string DecisionCode { get; set; } = null!;
        public int UserId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime SlaDueAt { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AppealDetailDto
    {
        public int RiskAppealId { get; set; }
        public string Reason { get; set; } = null!;
        public List<EvidenceFileDto> Evidence { get; set; } = new();
        public string Status { get; set; } = null!;
        public DateTime SlaDueAt { get; set; }
        public bool IsOverdue { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
        public JsonElement? AiSummary { get; set; }
        public DateTime CreatedAt { get; set; }

        public RiskDecisionAdminDto Decision { get; set; } = null!;
    }

    public class ResolveAppealRequest
    {
        public string Decision { get; set; } = null!;
        public string ReviewNote { get; set; } = null!;
    }
}