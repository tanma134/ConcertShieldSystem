using Microsoft.AspNetCore.Cors.Infrastructure;

namespace AdminAPI.Services.Risks;

public sealed class RiskScoreCalculator
{
    public const string HrWhitelist = "HR_WL";
    public const string HrBlacklistCard = "HR_BL01";
    public const string HrBlacklistDevice = "HR_BL02";
    public const string HrBlacklistIp = "HR_BL03";

    public ScoreResult CalculateScore(
        RiskConfig cfg,
        IEnumerable<MatchedRule> matched,
        IEnumerable<MatchedRule>? trust = null,
        bool trustBlocked = false)
    {
        var rules = DedupByCode(matched.Where(r => r.Score > 0)).ToList();

        var byGroup = rules
            .GroupBy(r => r.Group, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Math.Min(cfg.CapGroup, g.Sum(r => r.Score)),
                          StringComparer.OrdinalIgnoreCase);

        var raw = byGroup.Values.Sum();
        var groupCount = byGroup.Count;

        var combo = groupCount >= cfg.ComboMinGroups;
        if (combo) raw += cfg.BonusCombo;

        var trustDisabled = trustBlocked || rules.Any(r => r.DisableTrust);
        var trustPoints = trustDisabled
            ? 0
            : Math.Min(cfg.CapTrust, DedupByCode(trust ?? Array.Empty<MatchedRule>()).Sum(r => Math.Abs(r.Score)));

        var score = Math.Clamp(raw - trustPoints, 0, 100);
        return new ScoreResult(score, raw, trustPoints, byGroup, groupCount, combo, trustDisabled);
    }

    public RiskDecisionResult Decide(RiskConfig cfg, ScoreResult bot, ScoreResult fraud, DecisionContext? ctx = null)
    {
        ctx ??= new DecisionContext();
        var top = bot.Score >= fraud.Score ? bot : fraud;
        var dominant = bot.Score >= fraud.Score ? RiskType.Bot : RiskType.Fraud;

        if (ctx.HardRule == HrWhitelist)
            return Result(cfg, RiskLevel.Low, RiskAction.Allow, dominant, top.Score, ctx.HardRule, "HARD_RULE_WHITELIST");

        if (ctx.HardRule is HrBlacklistCard or HrBlacklistDevice or HrBlacklistIp)
        {
            var hrDominant = ctx.HardRule == HrBlacklistCard ? RiskType.Fraud : RiskType.Bot;
            var scopes = ctx.HardRule switch
            {
                HrBlacklistCard => new[] { "ACCOUNT" },
                HrBlacklistDevice => new[] { "DEVICE" },
                _ => new[] { "IP" }
            };
            return BuildHardRuleBlock(cfg, hrDominant, scopes, ctx, $"HARD_RULE_{ctx.HardRule}");
        }

        var score = top.Score;
        var level = LevelOf(cfg, score);
        var downgraded = false;

        if (level == RiskLevel.Critical && top.GroupCount < cfg.CriticalMinGroups)
        {
            level = RiskLevel.High;
            downgraded = true;
        }

        if (ctx.IsVipOrLargeOrder && score >= cfg.ThresholdHigh)
            return Result(cfg, RiskLevel.High, RiskAction.Hold, dominant, score, "HR_VIP", "VIP_FORCED_HIGH", downgraded);

        switch (level)
        {
            case RiskLevel.Low:
                return Result(cfg, level, RiskAction.Allow, dominant, score, null, "SCORE_LOW");
            case RiskLevel.Medium:
                return Result(cfg, level, RiskAction.Challenge, dominant, score, null, "SCORE_MEDIUM",
                    challenge: dominant == RiskType.Fraud ? "OTP" : "CAPTCHA");
            case RiskLevel.High:
                return Result(cfg, level, RiskAction.Hold, dominant, score, null,
                    downgraded ? "DOWNGRADED_SINGLE_GROUP" : "SCORE_HIGH", downgraded);
            default:
                var scopes = dominant == RiskType.Bot
                    ? new[] { "SESSION", "IP", "DEVICE" }
                    : new[] { "ACCOUNT" };
                return BuildBlock(cfg, RiskLevel.Critical, dominant, score, scopes, ctx, false, "SCORE_CRITICAL");
        }
    }

    public static RiskLevel LevelOf(RiskConfig cfg, int score) =>
        score >= cfg.ThresholdCritical ? RiskLevel.Critical :
        score >= cfg.ThresholdHigh ? RiskLevel.High :
        score >= cfg.ThresholdMedium ? RiskLevel.Medium : RiskLevel.Low;

    private static RiskDecisionResult BuildBlock(RiskConfig cfg, RiskLevel level, RiskType dominant, int score,
        string[] scopes, DecisionContext ctx, bool downgraded, string reason)
    {
        var escalation = Math.Min(3, Math.Max(0, ctx.PreviousBlocksInWindow) + 1);
        if (escalation >= 3)
        {
            return new RiskDecisionResult(level, RiskAction.Hold, dominant, score, Array.Empty<string>(),
                null, 3, true, null, ctx.HardRule, downgraded, cfg.IsShadow, "ESCALATION_MANUAL_REVIEW");
        }

        var hours = escalation == 1 ? cfg.BlockHoursLevel1 : cfg.BlockHoursLevel2;
        return new RiskDecisionResult(level, RiskAction.Block, dominant, score, scopes,
            hours, escalation, false, null, ctx.HardRule, downgraded, cfg.IsShadow, reason);
    }

    private static RiskDecisionResult BuildHardRuleBlock(RiskConfig cfg, RiskType dominant,
        string[] scopes, DecisionContext ctx, string reason)
    {
        var escalation = Math.Min(3, Math.Max(0, ctx.PreviousBlocksInWindow) + 1);
        var hours = escalation == 1 ? cfg.BlockHoursLevel1 : cfg.BlockHoursLevel2;
        return new RiskDecisionResult(RiskLevel.Critical, RiskAction.Block, dominant, 100, scopes,
            hours, escalation, escalation >= 3, null, ctx.HardRule, false, cfg.IsShadow, reason);
    }

    private static RiskDecisionResult Result(RiskConfig cfg, RiskLevel level, RiskAction action, RiskType dominant,
        int score, string? hardRule, string reason, bool downgraded = false, string? challenge = null) =>
        new(level, action, dominant, score, Array.Empty<string>(), null, 1, false, challenge,
            hardRule, downgraded, cfg.IsShadow, reason);

    private static IEnumerable<MatchedRule> DedupByCode(IEnumerable<MatchedRule> rules) =>
        rules.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
             .Select(g => g.OrderByDescending(r => Math.Abs(r.Score)).First());
}