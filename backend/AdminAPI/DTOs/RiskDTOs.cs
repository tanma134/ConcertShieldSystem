using System.Text.Json;

namespace AdminAPI.DTOs
{
   
    public class RuleHitDto
    {
        public string Code { get; set; } = null!;   
        public int Level { get; set; } = 1;        
        public string? Value { get; set; }         
    }

    public class IngestRiskEventRequest
    {
        public int? UserId { get; set; }
        public int? OrderId { get; set; }
        public int? TicketId { get; set; }
        public int? EventId { get; set; }
        public string? SessionId { get; set; }
        public string? DeviceFp { get; set; }
        public string? IpAddress { get; set; }
        public string? CardHash { get; set; }

        public List<RuleHitDto> BotRules { get; set; } = new();
        public List<RuleHitDto> FraudRules { get; set; } = new();
        public List<RuleHitDto> TrustRules { get; set; } = new();

        public bool IsVipCustomer { get; set; }
        public decimal? OrderAmount { get; set; }
        public List<string> SignalsMissing { get; set; } = new();
    }

    public class IngestRiskEventResponse
    {
        public string? DecisionCode { get; set; }
        public List<string> DecisionCodes { get; set; } = new();
        public int? FraudAlertId { get; set; }
        public string Level { get; set; } = null!;
        public string Action { get; set; } = null!;   
        public string Dominant { get; set; } = null!;
        public int Score { get; set; }
        public int BotScore { get; set; }
        public int FraudScore { get; set; }
        public List<string> Scopes { get; set; } = new();
        public int? BlockHours { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool RequiresManualReview { get; set; }
        public string? ChallengeType { get; set; }      
        public bool IsShadow { get; set; }          
        public string ReasonCode { get; set; } = null!;
        public List<string> IgnoredRules { get; set; } = new();
    }


    public class FraudAlertQuery
    {
        public string? RiskLevel { get; set; }
        public string? AlertType { get; set; }
        public string? Status { get; set; }
        public int? UserId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class CountItemDto { public string Key { get; set; } = null!; public int Count { get; set; } }

    public class FraudAlertSummaryDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Total { get; set; }
        public int OpenHighOrCritical { get; set; }
        public int OverdueSla { get; set; }
        public List<CountItemDto> ByLevel { get; set; } = new();
        public List<CountItemDto> ByType { get; set; } = new();
        public List<CountItemDto> ByStatus { get; set; } = new();
    }

    public class FraudAlertListItemDto
    {
        public int FraudAlertId { get; set; }
        public int? UserId { get; set; }
        public string AlertType { get; set; } = null!;
        public string RiskLevel { get; set; } = null!;
        public decimal RiskScore { get; set; }
        public decimal BotScore { get; set; }
        public decimal FraudScore { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public class RiskDecisionDetailDto
    {
        public int RiskDecisionId { get; set; }
        public string DecisionCode { get; set; } = null!;
        public string RiskLevel { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string? Scope { get; set; }
        public string Status { get; set; } = null!;
        public bool IsShadow { get; set; }
        public decimal BotScore { get; set; }
        public decimal FraudScore { get; set; }
        public string? DominantType { get; set; }
        public string? HardRule { get; set; }
        public string RuleVersion { get; set; } = null!;
        public string? ReasonCode { get; set; }          
        public int EscalationLevel { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string DecidedByType { get; set; } = null!;
        public int? DecidedBy { get; set; }
        public string? ManualReason { get; set; }
        public int? RestoredBy { get; set; }
        public DateTime? RestoredAt { get; set; }
        public string? RestoreReason { get; set; }
        public string? SessionId { get; set; }
        public string? DeviceFp { get; set; }
        public string? IpAddress { get; set; }
        public JsonElement? ScoreByGroup { get; set; }
        public JsonElement? TriggeredRules { get; set; }
        public JsonElement? TrustModifiers { get; set; }
        public JsonElement? SignalsMissing { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class FraudAlertDetailDto
    {
        public FraudAlertListItemDto Alert { get; set; } = null!;
        public int? OrderId { get; set; }
        public int? TicketId { get; set; }
        public int? EventId { get; set; }
        public string? SessionId { get; set; }
        public string? Details { get; set; }
        public JsonElement? AiSummary { get; set; }
        public List<RiskDecisionDetailDto> Decisions { get; set; } = new();
    }


    public class BlockRequest
    {
        public int? FraudAlertId { get; set; }
        public string Scope { get; set; } = null!;  
        public int? UserId { get; set; }
        public int? TicketId { get; set; }
        public string? SessionId { get; set; }
        public string? DeviceFp { get; set; }
        public string? IpAddress { get; set; }
        public int DurationHours { get; set; }
        public string Reason { get; set; } = null!;
    }

    public class RestoreRequest
    {
        public string Reason { get; set; } = null!;
    }

    public class RiskBlockQuery
    {
        public string? Status { get; set; }       
        public string? Scope { get; set; }
        public int? UserId { get; set; }
        public int? FraudAlertId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class RiskBlockItemDto
    {
        public int RiskDecisionId { get; set; }
        public string DecisionCode { get; set; } = null!;
        public int? FraudAlertId { get; set; }
        public int? UserId { get; set; }
        public string? Scope { get; set; }
        public string Status { get; set; } = null!;
        public bool IsShadow { get; set; }
        public string DecidedByType { get; set; } = null!;
        public int? DecidedBy { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RiskBlockResultDto
    {
        public int RiskDecisionId { get; set; }
        public string DecisionCode { get; set; } = null!;
        public int? FraudAlertId { get; set; }
        public int? UserId { get; set; }
        public string? Scope { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? ExpiresAt { get; set; }
    }
}