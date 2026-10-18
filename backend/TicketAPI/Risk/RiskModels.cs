namespace TicketAPI.Risk;

/// <summary>Một rule đã khớp. Code phải trùng mã trong bảng tham số của AdminAPI (vd BOT_H01, FRD_T01, TRU_01).</summary>
public class RuleHit
{
    public string Code { get; set; } = "";
    public int Level { get; set; } = 1;      // rule có mức 1/2 (BOT_V01, BOT_S01) thì truyền 1 hoặc 2
    public string? Value { get; set; }       // bằng chứng ngắn hiển thị cho Admin, vd "8 tickets"
    public RuleHit() { }
    public RuleHit(string code, int level = 1, string? value = null) { Code = code; Level = level; Value = value; }
}

/// <summary>Khớp IngestRiskEventRequest của AdminAPI.</summary>
public class RiskEvent
{
    public int? UserId { get; set; }
    public int? OrderId { get; set; }
    public int? TicketId { get; set; }
    public int? EventId { get; set; }
    public string? SessionId { get; set; }
    public string? DeviceFp { get; set; }
    public string? IpAddress { get; set; }
    public string? CardHash { get; set; }
    public List<RuleHit> BotRules { get; set; } = new();
    public List<RuleHit> FraudRules { get; set; } = new();
    public List<RuleHit> TrustRules { get; set; } = new();
    public bool IsVipCustomer { get; set; }
    public decimal? OrderAmount { get; set; }
    public List<string> SignalsMissing { get; set; } = new();
}

/// <summary>Khớp IngestRiskEventResponse của AdminAPI.</summary>
public class RiskResult
{
    public string? DecisionCode { get; set; }
    public int? FraudAlertId { get; set; }
    public string Level { get; set; } = "Low";
    public string Action { get; set; } = "ALLOW";   // ALLOW | CHALLENGE | HOLD | BLOCK
    public string? Dominant { get; set; }
    public int Score { get; set; }
    public int BotScore { get; set; }
    public int FraudScore { get; set; }
    public List<string> Scopes { get; set; } = new();
    public int? BlockHours { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool RequiresManualReview { get; set; }
    public string? ChallengeType { get; set; }
    public bool IsShadow { get; set; }
    public string? ReasonCode { get; set; }
    public List<string> IgnoredRules { get; set; } = new();
}

public class BlockCheckResult
{
    public bool Blocked { get; set; }
    public string? Scope { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Message { get; set; }
}
