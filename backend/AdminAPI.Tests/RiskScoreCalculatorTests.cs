using AdminAPI.Services.Risks;
using Xunit;

namespace AdminAPI.Tests;

public class RiskScoreCalculatorTests
{
    private readonly RiskConfig _cfg = new();          // mặc định khớp dữ liệu seed
    private readonly RiskScoreCalculator _calc = new();

    private static MatchedRule R(string code, string group, int score, bool disableTrust = false) =>
        new(code, group, score, null, disableTrust);

    private static readonly MatchedRule Tru01 = new("TRU_01", "T", -15);
    private static readonly MatchedRule Tru02 = new("TRU_02", "T", -10);
    private static readonly MatchedRule Tru03 = new("TRU_03", "T", -10);

    private ScoreResult Score(IEnumerable<MatchedRule> m, IEnumerable<MatchedRule>? t = null, bool blocked = false)
        => _calc.CalculateScore(_cfg, m, t, blocked);

    // ------------------------------------------------ 3 ca kiểm tra nhanh

    [Fact]
    public void Ca1_BotCurl_12reqPerSec_IsCritical()
    {
        // H01 +30, H05 +25 (nhóm H = 55 -> cắt 40), V01 mức 2 +35, E02 +15
        var bot = Score(new[] { R("BOT_H01", "H", 30), R("BOT_H05", "H", 25), R("BOT_V01", "V", 35), R("BOT_E02", "E", 15) });

        Assert.Equal(40, bot.ByGroup["H"]);
        Assert.Equal(35, bot.ByGroup["V"]);
        Assert.Equal(15, bot.ByGroup["E"]);
        Assert.Equal(100, bot.Raw);          // 40 + 35 + 15 + 10 combo
        Assert.True(bot.ComboApplied);
        Assert.Equal(100, bot.Score);

        var d = _calc.Decide(_cfg, bot, Score(Array.Empty<MatchedRule>()));
        Assert.Equal(RiskLevel.Critical, d.Level);
        Assert.Equal(RiskAction.Block, d.Action);
        Assert.Equal(RiskType.Bot, d.Dominant);
        Assert.Equal(24, d.BlockHours);
        Assert.Equal(new[] { "SESSION", "IP", "DEVICE" }, d.Scopes);
    }

    [Fact]
    public void Ca2_RegularCustomerOnVpn_IsLow()
    {
        var bot = Score(new[] { R("BOT_E02", "E", 15), R("BOT_V02", "V", 20) }, new[] { Tru01, Tru02, Tru03 });

        Assert.Equal(35, bot.Raw);
        Assert.False(bot.ComboApplied);      // chỉ 2 nhóm
        Assert.Equal(30, bot.Trust);         // -35 nhưng trần 30
        Assert.Equal(5, bot.Score);

        var d = _calc.Decide(_cfg, bot, Score(Array.Empty<MatchedRule>()));
        Assert.Equal(RiskLevel.Low, d.Level);
        Assert.Equal(RiskAction.Allow, d.Action);
    }

    [Fact]
    public void Ca2b_SameBehaviorNewAccount_IsMediumCaptcha()
    {
        var bot = Score(new[] { R("BOT_E02", "E", 15), R("BOT_V02", "V", 20) });
        Assert.Equal(35, bot.Score);

        var d = _calc.Decide(_cfg, bot, Score(Array.Empty<MatchedRule>()));
        Assert.Equal(RiskLevel.Medium, d.Level);
        Assert.Equal(RiskAction.Challenge, d.Action);
        Assert.Equal("CAPTCHA", d.ChallengeType);
    }

    [Fact]
    public void Ca3_CardTesting_NewAccount_IsHigh_Hold()
    {
        // P01 +25 + P04 +35 = 60 -> cắt 40; A01 +20
        var fraud = Score(new[] { R("FRD_P01", "P", 25), R("FRD_P04", "P", 35), R("FRD_A01", "A", 20) });

        Assert.Equal(40, fraud.ByGroup["P"]);
        Assert.Equal(20, fraud.ByGroup["A"]);
        Assert.Equal(60, fraud.Score);       // 2 nhóm nên không combo

        var d = _calc.Decide(_cfg, Score(Array.Empty<MatchedRule>()), fraud);
        Assert.Equal(RiskLevel.High, d.Level);
        Assert.Equal(RiskAction.Hold, d.Action);
        Assert.Equal(RiskType.Fraud, d.Dominant);
    }

    [Fact]
    public void CardTesting_WithNewDeviceAndCountry_BecomesCritical_AccountBlock()
    {
        var fraud = Score(new[]
        {
            R("FRD_P01", "P", 25), R("FRD_P04", "P", 35), R("FRD_A01", "A", 20), R("FRD_G01", "G", 15)
        });
        Assert.Equal(85, fraud.Score);       // 40 + 20 + 15 + 10

        var d = _calc.Decide(_cfg, Score(Array.Empty<MatchedRule>()), fraud);
        Assert.Equal(RiskLevel.Critical, d.Level);
        Assert.Equal(RiskAction.Block, d.Action);
        Assert.Equal(new[] { "ACCOUNT" }, d.Scopes);
    }

    // ------------------------------------------------ các quy tắc của công thức

    [Fact]
    public void SameRuleMatchedManyTimes_CountsOnce()
    {
        var r = Score(new[] { R("BOT_H01", "H", 30), R("BOT_H01", "H", 30), R("BOT_H01", "H", 30) });
        Assert.Equal(30, r.Raw);
    }

    [Fact]
    public void GroupCap_LimitsEachGroupTo40()
    {
        var r = Score(new[] { R("BOT_H01", "H", 30), R("BOT_H02", "H", 20), R("BOT_H05", "H", 25) });
        Assert.Equal(40, r.ByGroup["H"]);
        Assert.Equal(40, r.Score);
    }

    [Fact]
    public void Combo_Needs3Groups()
    {
        var two = Score(new[] { R("A", "V", 10), R("B", "H", 10) });
        var three = Score(new[] { R("A", "V", 10), R("B", "H", 10), R("C", "E", 10) });
        Assert.False(two.ComboApplied);
        Assert.Equal(20, two.Raw);
        Assert.True(three.ComboApplied);
        Assert.Equal(40, three.Raw);         // 30 + 10
    }

    [Fact]
    public void Score_IsClampedTo0And100()
    {
        var negative = Score(new[] { R("A", "V", 10) }, new[] { Tru01, Tru02, Tru03 });
        Assert.Equal(0, negative.Score);     // 10 - 30 = -20 -> 0

        var cfg = new RiskConfig { CapGroup = 500 };
        var high = _calc.CalculateScore(cfg, new[] { R("A", "V", 300), R("B", "H", 300) });
        Assert.Equal(100, high.Score);
    }

    [Fact]
    public void Trust_NotApplied_WhenChargebackHistory()
    {
        var r = Score(new[] { R("FRD_L01", "L", 35, disableTrust: true) }, new[] { Tru01, Tru02, Tru03 });
        Assert.True(r.TrustDisabled);
        Assert.Equal(0, r.Trust);
        Assert.Equal(35, r.Score);
    }

    [Fact]
    public void Trust_NotApplied_WhenHardRuleFlag()
    {
        var r = Score(new[] { R("A", "V", 40) }, new[] { Tru01 }, blocked: true);
        Assert.Equal(40, r.Score);
    }

    // ------------------------------------------------ ngưỡng và hạ mức

    [Theory]
    [InlineData(0, RiskLevel.Low)]
    [InlineData(29, RiskLevel.Low)]
    [InlineData(30, RiskLevel.Medium)]
    [InlineData(59, RiskLevel.Medium)]
    [InlineData(60, RiskLevel.High)]
    [InlineData(79, RiskLevel.High)]
    [InlineData(80, RiskLevel.Critical)]
    [InlineData(100, RiskLevel.Critical)]
    public void Thresholds_BoundaryValues(int score, RiskLevel expected)
    {
        Assert.Equal(expected, RiskScoreCalculator.LevelOf(_cfg, score));
    }

    [Fact]
    public void Critical_WithSingleGroup_IsDowngradedToHigh()
    {
        // Admin nâng trần nhóm lên 90: 1 nhóm đạt 85 -> không được auto-block
        var cfg = new RiskConfig { CapGroup = 90 };
        var bot = _calc.CalculateScore(cfg, new[] { R("BOT_H01", "H", 30), R("BOT_H05", "H", 25), R("BOT_H02", "H", 30) });
        Assert.Equal(85, bot.Score);
        Assert.Equal(1, bot.GroupCount);

        var d = _calc.Decide(cfg, bot, _calc.CalculateScore(cfg, Array.Empty<MatchedRule>()));
        Assert.Equal(RiskLevel.High, d.Level);
        Assert.Equal(RiskAction.Hold, d.Action);
        Assert.True(d.Downgraded);
    }

    [Fact]
    public void Dominant_IsFraud_WhenFraudScoreHigher()
    {
        var bot = Score(new[] { R("A", "V", 20) });
        var fraud = Score(new[] { R("B", "P", 35) });
        var d = _calc.Decide(_cfg, bot, fraud);
        Assert.Equal(RiskType.Fraud, d.Dominant);
        Assert.Equal("OTP", d.ChallengeType);   // Medium + nghi fraud thì OTP
    }

    // ------------------------------------------------ luật cứng

    [Fact]
    public void HardRule_Whitelist_AlwaysAllow_EvenWithHighScore()
    {
        var bot = Score(new[] { R("A", "V", 40), R("B", "H", 40), R("C", "E", 30) });
        var d = _calc.Decide(_cfg, bot, Score(Array.Empty<MatchedRule>()), new DecisionContext(HardRule: "HR_WL"));
        Assert.Equal(RiskAction.Allow, d.Action);
        Assert.Equal("HR_WL", d.HardRule);
    }

    [Theory]
    [InlineData("HR_BL01", "ACCOUNT", RiskType.Fraud)]
    [InlineData("HR_BL02", "DEVICE", RiskType.Bot)]
    [InlineData("HR_BL03", "IP", RiskType.Bot)]
    public void HardRule_Blacklist_BlocksImmediately_WithZeroScore(string hr, string scope, RiskType type)
    {
        var zero = Score(Array.Empty<MatchedRule>());
        var d = _calc.Decide(_cfg, zero, zero, new DecisionContext(HardRule: hr));
        Assert.Equal(RiskAction.Block, d.Action);
        Assert.Equal(RiskLevel.Critical, d.Level);
        Assert.Equal(new[] { scope }, d.Scopes);
        Assert.Equal(type, d.Dominant);
    }

    [Fact]
    public void HardRule_Vip_ForcesHoldInsteadOfBlock()
    {
        var fraud = Score(new[] { R("FRD_P01", "P", 25), R("FRD_P04", "P", 35), R("FRD_A01", "A", 20), R("FRD_G01", "G", 15) });
        Assert.Equal(85, fraud.Score);

        var d = _calc.Decide(_cfg, Score(Array.Empty<MatchedRule>()), fraud, new DecisionContext(IsVipOrLargeOrder: true));
        Assert.Equal(RiskLevel.High, d.Level);
        Assert.Equal(RiskAction.Hold, d.Action);
        Assert.Equal("HR_VIP", d.HardRule);
    }

    [Fact]
    public void Vip_BelowHighThreshold_IsNotForced()
    {
        var bot = Score(new[] { R("A", "V", 35) });
        var d = _calc.Decide(_cfg, bot, Score(Array.Empty<MatchedRule>()), new DecisionContext(IsVipOrLargeOrder: true));
        Assert.Equal(RiskLevel.Medium, d.Level);
    }

    // ------------------------------------------------ leo thang block

    [Theory]
    [InlineData(0, 24, 1, false)]
    [InlineData(1, 72, 2, false)]
    [InlineData(2, null, 3, true)]
    [InlineData(5, null, 3, true)]
    public void Escalation_24h_72h_ThenManual(int previousBlocks, int? hours, int level, bool manual)
    {
        var bot = Score(new[] { R("A", "H", 40), R("B", "V", 40), R("C", "E", 15) });   // 95 + 10 -> 100
        var d = _calc.Decide(_cfg, bot, Score(Array.Empty<MatchedRule>()),
            new DecisionContext(PreviousBlocksInWindow: previousBlocks));

        Assert.Equal(hours, d.BlockHours);
        Assert.Equal(level, d.EscalationLevel);
        Assert.Equal(manual, d.RequiresManualReview);
        Assert.Equal(manual ? RiskAction.Hold : RiskAction.Block, d.Action);   // không bao giờ block vĩnh viễn tự động
    }

    [Fact]
    public void ShadowMode_IsReportedInDecision()
    {
        var shadow = _calc.Decide(new RiskConfig { Mode = "SHADOW" }, Score(Array.Empty<MatchedRule>()), Score(Array.Empty<MatchedRule>()));
        var enforce = _calc.Decide(new RiskConfig { Mode = "ENFORCE" }, Score(Array.Empty<MatchedRule>()), Score(Array.Empty<MatchedRule>()));
        Assert.True(shadow.IsShadow);
        Assert.False(enforce.IsShadow);
    }

    // ------------------------------------------------ đọc cấu hình từ system_parameters

    [Fact]
    public void ConfigParse_ReadsNumbersAndRules()
    {
        var rows = new Dictionary<string, string>
        {
            ["risk.cap.group"] = "50",
            ["risk.mode"] = "enforce",
            ["risk.rule.BOT_V01"] = "{\"type\":\"BOT\",\"group\":\"V\",\"window_sec\":10,\"threshold\":5,\"score\":25,\"threshold_l2\":10,\"score_l2\":35,\"enabled\":true}",
            ["risk.rule.BOT_H01"] = "{\"type\":\"BOT\",\"group\":\"H\",\"score\":30,\"enabled\":true,\"ua_list\":[\"curl\",\"wget\"]}",
            ["risk.rule.BOT_X99"] = "{ khong phai json",
            ["risk.threshold.high"] = "abc"
        };

        var cfg = RiskConfig.Parse(rows);

        Assert.Equal(50, cfg.CapGroup);
        Assert.Equal("ENFORCE", cfg.Mode);
        Assert.Equal(60, cfg.ThresholdHigh);                       // giá trị lỗi -> giữ mặc định
        Assert.Equal(2, cfg.Rules.Count);                          // rule lỗi bị bỏ qua
        Assert.Equal(2, cfg.ParseWarnings.Count);

        var v01 = cfg.Rules["BOT_V01"];
        Assert.True(v01.HasLevel2);
        Assert.Equal(25, v01.ScoreForLevel(1));
        Assert.Equal(35, v01.ScoreForLevel(2));                    // 12 req/s -> lấy mức 2

        Assert.Equal(new[] { "curl", "wget" }, cfg.Rules["BOT_H01"].GetStringList("ua_list"));
    }
}
