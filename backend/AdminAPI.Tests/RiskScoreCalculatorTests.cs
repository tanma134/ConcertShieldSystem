using AdminAPI.Services.Risks;
using Xunit;

namespace AdminAPI.Tests;

/// <summary>
/// Kiểm tra bộ chấm điểm theo đúng tài liệu "Bộ luật và cách tính điểm":
/// 5 ví dụ tính tay + các quy tắc trần nhóm, combo, trust, luật cứng, leo thang.
/// Dùng RiskConfig mặc định: trần nhóm 40, trần trust 30, combo +10 từ 3 nhóm, ngưỡng 30/60/80.
/// </summary>
public class RiskScoreCalculatorTests
{
    private readonly RiskScoreCalculator _calc = new();
    private readonly RiskConfig _cfg = new();

    private static MatchedRule R(string code, string group, int score, bool disableTrust = false)
        => new(code, group, score, null, disableTrust);

    private ScoreResult Score(IEnumerable<MatchedRule> rules, IEnumerable<MatchedRule>? trust = null)
        => _calc.CalculateScore(_cfg, rules, trust);

    private ScoreResult Empty() => Score(Array.Empty<MatchedRule>());

    // ------------------------------------------------------------ 5 ví dụ trong tài liệu

    [Fact]
    public void Example1_OneBotRule_IsMedium_Captcha()
    {
        var bot = Score(new[] { R("BOT_H01", "H", 30) });

        Assert.Equal(30, bot.Score);
        Assert.False(bot.ComboApplied);

        var d = _calc.Decide(_cfg, bot, Empty());
        Assert.Equal(RiskLevel.Medium, d.Level);
        Assert.Equal(RiskAction.Challenge, d.Action);
        Assert.Equal(RiskType.Bot, d.Dominant);
        Assert.Equal("CAPTCHA", d.ChallengeType);
    }

    [Fact]
    public void Example2_GroupCapStopsOneSignalFromDominating_High_Hold()
    {
        var fraud = Score(new[]
        {
            R("FRD_P01", "P", 25),
            R("FRD_P04", "P", 35),   // nhóm P: 25 + 35 = 60, bị cắt còn 40
            R("FRD_A01", "A", 20),
        });

        Assert.Equal(40, fraud.ByGroup["P"]);
        Assert.Equal(20, fraud.ByGroup["A"]);
        Assert.False(fraud.ComboApplied);     // chỉ 2 nhóm
        Assert.Equal(60, fraud.Score);        // không phải 80 như khi cộng thẳng

        var d = _calc.Decide(_cfg, Empty(), fraud);
        Assert.Equal(RiskLevel.High, d.Level);
        Assert.Equal(RiskAction.Hold, d.Action);
        Assert.Equal(RiskType.Fraud, d.Dominant);
    }

    [Fact]
    public void Example3_ThreeGroupsGetCombo_ScoreCappedAt100_Critical_Block()
    {
        var bot = Score(new[]
        {
            R("BOT_H01", "H", 30),
            R("BOT_H05", "H", 25),   // H: 55 -> 40
            R("BOT_V01", "V", 35),   // V01 mức 2
            R("BOT_E02", "E", 15),
        });

        Assert.Equal(40, bot.ByGroup["H"]);
        Assert.Equal(3, bot.GroupCount);
        Assert.True(bot.ComboApplied);
        Assert.Equal(100, bot.Raw);           // 40 + 35 + 15 + 10, thay vì 105 nếu cộng thẳng
        Assert.Equal(100, bot.Score);

        var d = _calc.Decide(_cfg, bot, Empty());
        Assert.Equal(RiskLevel.Critical, d.Level);
        Assert.Equal(RiskAction.Block, d.Action);
        Assert.Equal(RiskType.Bot, d.Dominant);
        Assert.Equal(new[] { "SESSION", "IP", "DEVICE" }, d.Scopes);
        Assert.Equal(24, d.BlockHours);
        Assert.Equal(1, d.EscalationLevel);
    }

    [Fact]
    public void Example4_TrustPointsLowerHighToMedium_Otp()
    {
        var rules = new[] { R("FRD_P04", "P", 35), R("FRD_A03", "A", 30) };   // 65
        var trust = new[] { R("TRU_01", "TRUST", 15), R("TRU_05", "TRUST", 20) };   // 35, cắt còn 30

        Assert.Equal(65, Score(rules).Score);

        var fraud = Score(rules, trust);
        Assert.Equal(30, fraud.Trust);
        Assert.Equal(35, fraud.Score);

        var d = _calc.Decide(_cfg, Empty(), fraud);
        Assert.Equal(RiskLevel.Medium, d.Level);
        Assert.Equal(RiskAction.Challenge, d.Action);
        Assert.Equal("OTP", d.ChallengeType);        // Fraud -> OTP, Bot -> CAPTCHA
    }

    [Fact]
    public void Example5_BlacklistedCard_IsHardRuleBlockOnAccount()
    {
        var d = _calc.Decide(_cfg, Empty(), Empty(), new DecisionContext(HardRule: "HR_BL01"));

        Assert.Equal(RiskLevel.Critical, d.Level);
        Assert.Equal(RiskAction.Block, d.Action);
        Assert.Equal(100, d.Score);
        Assert.Equal(new[] { "ACCOUNT" }, d.Scopes);
        Assert.Equal(RiskType.Fraud, d.Dominant);
    }

    // ------------------------------------------------------------ quy tắc chấm điểm

    [Fact]
    public void SingleGroup_IsCappedAt40()
    {
        var s = Score(new[] { R("A", "H", 30), R("B", "H", 30), R("C", "H", 30) });
        Assert.Equal(40, s.Score);
    }

    [Fact]
    public void TwoGroups_NoCombo_ThreeGroups_AddCombo()
    {
        var two = Score(new[] { R("A", "H", 20), R("B", "V", 20) });
        Assert.False(two.ComboApplied);
        Assert.Equal(40, two.Score);

        var three = Score(new[] { R("A", "H", 20), R("B", "V", 20), R("C", "E", 20) });
        Assert.True(three.ComboApplied);
        Assert.Equal(70, three.Score);               // 60 + 10
    }

    [Fact]
    public void SameRuleCodeIsCountedOnce()
    {
        var s = Score(new[] { R("BOT_H01", "H", 30), R("BOT_H01", "H", 30) });
        Assert.Equal(30, s.Score);
    }

    [Fact]
    public void TrustIsCappedAt30_AndScoreNeverBelowZero()
    {
        var trust = new[] { R("T1", "TRUST", 15), R("T2", "TRUST", 15), R("T3", "TRUST", 15) };   // 45 -> 30
        var s = Score(new[] { R("BOT_H03", "H", 10) }, trust);

        Assert.Equal(30, s.Trust);
        Assert.Equal(0, s.Score);                    // 10 - 30 bị chặn ở 0
    }

    [Fact]
    public void RuleThatDisablesTrust_Chargeback_IgnoresTrustPoints()
    {
        var rules = new[] { R("FRD_P04", "P", 35), R("FRD_L01", "L", 35, disableTrust: true) };
        var trust = new[] { R("TRU_01", "TRUST", 15) };

        var s = Score(rules, trust);

        Assert.True(s.TrustDisabled);
        Assert.Equal(0, s.Trust);
        Assert.Equal(70, s.Score);
    }

    [Fact]
    public void TrustIsIgnoredWhenAHardRuleBlocks()
    {
        var s = _calc.CalculateScore(_cfg, new[] { R("A", "H", 30) }, new[] { R("TRU_01", "TRUST", 15) }, trustBlocked: true);
        Assert.Equal(0, s.Trust);
        Assert.Equal(30, s.Score);
    }

    [Fact]
    public void ZeroScoreRulesAreIgnored()
    {
        var s = Score(new[] { R("A", "H", 0), R("B", "V", 0), R("C", "E", 0) });
        Assert.Equal(0, s.GroupCount);
        Assert.Equal(0, s.Score);
    }

    // ------------------------------------------------------------ ngưỡng và hành động

    [Theory]
    [InlineData(0, RiskLevel.Low)]
    [InlineData(29, RiskLevel.Low)]
    [InlineData(30, RiskLevel.Medium)]
    [InlineData(59, RiskLevel.Medium)]
    [InlineData(60, RiskLevel.High)]
    [InlineData(79, RiskLevel.High)]
    [InlineData(80, RiskLevel.Critical)]
    [InlineData(100, RiskLevel.Critical)]
    public void LevelOf_UsesThresholds(int score, RiskLevel expected)
        => Assert.Equal(expected, RiskScoreCalculator.LevelOf(_cfg, score));

    [Fact]
    public void LowScore_IsAllowed()
    {
        var d = _calc.Decide(_cfg, Score(new[] { R("BOT_H03", "H", 10) }), Empty());
        Assert.Equal(RiskLevel.Low, d.Level);
        Assert.Equal(RiskAction.Allow, d.Action);
    }

    [Fact]
    public void Tie_BotWinsOverFraud()
    {
        var bot = Score(new[] { R("B", "H", 40) });
        var fraud = Score(new[] { R("F", "P", 40) });
        Assert.Equal(RiskType.Bot, _calc.Decide(_cfg, bot, fraud).Dominant);
    }

    [Fact]
    public void FraudCritical_BlocksAccount()
    {
        var fraud = Score(new[] { R("F1", "P", 40), R("F2", "A", 40) });   // 80, 2 nhóm
        var d = _calc.Decide(_cfg, Empty(), fraud);

        Assert.Equal(RiskLevel.Critical, d.Level);
        Assert.Equal(RiskAction.Block, d.Action);
        Assert.Equal(new[] { "ACCOUNT" }, d.Scopes);
    }

    [Fact]
    public void CriticalWithOnlyOneGroup_IsDowngradedToHigh()
    {
        var cfg = new RiskConfig { CapGroup = 100 };      // bỏ trần để một nhóm vượt 80
        var bot = _calc.CalculateScore(cfg, new[] { R("A", "H", 50), R("B", "H", 40) });
        Assert.Equal(90, bot.Score);
        Assert.Equal(1, bot.GroupCount);

        var d = _calc.Decide(cfg, bot, _calc.CalculateScore(cfg, Array.Empty<MatchedRule>()));
        Assert.Equal(RiskLevel.High, d.Level);
        Assert.Equal(RiskAction.Hold, d.Action);
        Assert.True(d.Downgraded);
        Assert.Equal("DOWNGRADED_SINGLE_GROUP", d.ReasonCode);
    }

    // ------------------------------------------------------------ leo thang block

    [Fact]
    public void SecondBlockInWindow_Lasts72Hours()
    {
        var bot = Score(new[] { R("A", "H", 40), R("B", "V", 40) });
        var d = _calc.Decide(_cfg, bot, Empty(), new DecisionContext(PreviousBlocksInWindow: 1));

        Assert.Equal(RiskAction.Block, d.Action);
        Assert.Equal(72, d.BlockHours);
        Assert.Equal(2, d.EscalationLevel);
    }

    [Fact]
    public void ThirdBlockInWindow_GoesToManualReviewInsteadOfBlock()
    {
        var bot = Score(new[] { R("A", "H", 40), R("B", "V", 40) });
        var d = _calc.Decide(_cfg, bot, Empty(), new DecisionContext(PreviousBlocksInWindow: 2));

        Assert.Equal(RiskAction.Hold, d.Action);
        Assert.True(d.RequiresManualReview);
        Assert.Equal(3, d.EscalationLevel);
        Assert.Empty(d.Scopes);
    }

    // ------------------------------------------------------------ luật cứng

    [Fact]
    public void Whitelist_AlwaysAllows_EvenWithHighScore()
    {
        var bot = Score(new[] { R("A", "H", 40), R("B", "V", 40) });
        var d = _calc.Decide(_cfg, bot, Empty(), new DecisionContext(HardRule: "HR_WL"));

        Assert.Equal(RiskAction.Allow, d.Action);
        Assert.Equal(RiskLevel.Low, d.Level);
        Assert.Equal("HARD_RULE_WHITELIST", d.ReasonCode);
    }

    [Theory]
    [InlineData("HR_BL01", "ACCOUNT")]
    [InlineData("HR_BL02", "DEVICE")]
    [InlineData("HR_BL03", "IP")]
    public void Blacklists_BlockOnTheMatchingScope(string rule, string scope)
    {
        var d = _calc.Decide(_cfg, Empty(), Empty(), new DecisionContext(HardRule: rule));
        Assert.Equal(RiskAction.Block, d.Action);
        Assert.Equal(new[] { scope }, d.Scopes);
        Assert.Equal(100, d.Score);
    }

    [Fact]
    public void VipOrLargeOrder_WithHighScore_IsForcedToHold()
    {
        var fraud = Score(new[] { R("F1", "P", 40), R("F2", "A", 25) });     // 65
        var d = _calc.Decide(_cfg, Empty(), fraud, new DecisionContext(IsVipOrLargeOrder: true));

        Assert.Equal(RiskAction.Hold, d.Action);
        Assert.Equal("VIP_FORCED_HIGH", d.ReasonCode);
    }
}
