namespace AdminAPI.Services.Risks;

public enum RiskLevel { Low = 0, Medium = 1, High = 2, Critical = 3 }

public enum RiskAction { Allow, Challenge, Hold, Block }

public enum RiskType { Bot, Fraud }


public sealed record MatchedRule(
    string Code,              
    string Group,            
    int Score,                
    string? Value = null,     
    bool DisableTrust = false 
);

public sealed record ScoreResult(
    int Score,                                  
    int Raw,                                    
    int Trust,                                 
    IReadOnlyDictionary<string, int> ByGroup,  
    int GroupCount,                            
    bool ComboApplied,
    bool TrustDisabled);

public sealed record DecisionContext(
    string? HardRule = null,             
    bool IsVipOrLargeOrder = false,      
    int PreviousBlocksInWindow = 0);     

public sealed record RiskDecisionResult(
    RiskLevel Level,
    RiskAction Action,
    RiskType Dominant,
    int Score,
    IReadOnlyList<string> Scopes,        
    int? BlockHours,                     
    int EscalationLevel,                 
    bool RequiresManualReview,
    string? ChallengeType,               
    string? HardRule,
    bool Downgraded,                     
    bool IsShadow,                       
    string ReasonCode);