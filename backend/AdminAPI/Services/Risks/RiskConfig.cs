using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminAPI.Services.Risks;

public sealed class RiskRuleConfig
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("group")] public string Group { get; set; } = "";
    [JsonPropertyName("window_sec")] public int WindowSec { get; set; }
    [JsonPropertyName("threshold")] public double? Threshold { get; set; }
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("threshold_l2")] public double? ThresholdL2 { get; set; }
    [JsonPropertyName("score_l2")] public int? ScoreL2 { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("disable_trust")] public bool DisableTrust { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    public bool HasLevel2 => ThresholdL2.HasValue && ScoreL2.HasValue;

    public int ScoreForLevel(int level) => level >= 2 && ScoreL2.HasValue ? ScoreL2.Value : Score;

    public IReadOnlyList<string> GetStringList(string key)
    {
        if (Extra != null && Extra.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Array)
            return el.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                     .Select(x => x.GetString()!).ToList();
        return Array.Empty<string>();
    }

    public double? GetNumber(string key) =>
        Extra != null && Extra.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetDouble() : null;
}


public sealed class RiskConfig
{
    public string Mode { get; set; } = "SHADOW";
    public string RuleVersion { get; set; } = "v1.0";

    public int CapGroup { get; set; } = 40;
    public int CapTrust { get; set; } = 30;
    public int BonusCombo { get; set; } = 10;
    public int ComboMinGroups { get; set; } = 3;

    public int ThresholdMedium { get; set; } = 30;
    public int ThresholdHigh { get; set; } = 60;
    public int ThresholdCritical { get; set; } = 80;
    public int CriticalMinGroups { get; set; } = 2;

    public int BlockHoursLevel1 { get; set; } = 24;
    public int BlockHoursLevel2 { get; set; } = 72;
    public int EscalationWindowDays { get; set; } = 30;
    public int TrustRestoreDays { get; set; } = 30;

    public int AlertSlaHours { get; set; } = 2;
    public int AppealDeadlineDays { get; set; } = 7;
    public int AppealSlaHours { get; set; } = 48;
    public int AppealMaxEvidenceFiles { get; set; } = 5;
    public int AppealMaxEvidenceMb { get; set; } = 5;

    public bool AiEnabled { get; set; } = true;
    public int AiMinScore { get; set; } = 60;
    public decimal VipMinOrderAmount { get; set; } = 20_000_000m;

    public Dictionary<string, RiskRuleConfig> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> ParseWarnings { get; } = new();

    public bool IsShadow => string.Equals(Mode, "SHADOW", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<KeyValuePair<string, RiskRuleConfig>> EnabledRules(string type) =>
        Rules.Where(r => r.Value.Enabled && string.Equals(r.Value.Type, type, StringComparison.OrdinalIgnoreCase));

    private const string RulePrefix = "risk.rule.";

    
    public static RiskConfig Parse(IEnumerable<KeyValuePair<string, string>> rows)
    {
        var cfg = new RiskConfig();
        foreach (var (key, value) in rows)
        {
            if (key.StartsWith(RulePrefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var rule = JsonSerializer.Deserialize<RiskRuleConfig>(value)
                               ?? throw new JsonException("null");
                    cfg.Rules[key[RulePrefix.Length..]] = rule;
                }
                catch (JsonException)
                {
                    cfg.ParseWarnings.Add($"{key}: JSON không hợp lệ, bỏ qua rule");
                }
                continue;
            }

            switch (key)
            {
                case "risk.mode": cfg.Mode = value.Trim().ToUpperInvariant(); break;
                case "risk.rule_version": cfg.RuleVersion = value.Trim(); break;
                case "risk.cap.group": cfg.CapGroup = Int(cfg, key, value, cfg.CapGroup); break;
                case "risk.cap.trust": cfg.CapTrust = Int(cfg, key, value, cfg.CapTrust); break;
                case "risk.bonus.combo": cfg.BonusCombo = Int(cfg, key, value, cfg.BonusCombo); break;
                case "risk.combo.min_groups": cfg.ComboMinGroups = Int(cfg, key, value, cfg.ComboMinGroups); break;
                case "risk.threshold.medium": cfg.ThresholdMedium = Int(cfg, key, value, cfg.ThresholdMedium); break;
                case "risk.threshold.high": cfg.ThresholdHigh = Int(cfg, key, value, cfg.ThresholdHigh); break;
                case "risk.threshold.critical": cfg.ThresholdCritical = Int(cfg, key, value, cfg.ThresholdCritical); break;
                case "risk.critical.min_groups": cfg.CriticalMinGroups = Int(cfg, key, value, cfg.CriticalMinGroups); break;
                case "risk.block.hours.level1": cfg.BlockHoursLevel1 = Int(cfg, key, value, cfg.BlockHoursLevel1); break;
                case "risk.block.hours.level2": cfg.BlockHoursLevel2 = Int(cfg, key, value, cfg.BlockHoursLevel2); break;
                case "risk.block.escalation_window_days": cfg.EscalationWindowDays = Int(cfg, key, value, cfg.EscalationWindowDays); break;
                case "risk.trust.restore_days": cfg.TrustRestoreDays = Int(cfg, key, value, cfg.TrustRestoreDays); break;
                case "risk.alert.sla_hours": cfg.AlertSlaHours = Int(cfg, key, value, cfg.AlertSlaHours); break;
                case "risk.appeal.deadline_days": cfg.AppealDeadlineDays = Int(cfg, key, value, cfg.AppealDeadlineDays); break;
                case "risk.appeal.sla_hours": cfg.AppealSlaHours = Int(cfg, key, value, cfg.AppealSlaHours); break;
                case "risk.appeal.max_evidence_files": cfg.AppealMaxEvidenceFiles = Int(cfg, key, value, cfg.AppealMaxEvidenceFiles); break;
                case "risk.appeal.max_evidence_mb": cfg.AppealMaxEvidenceMb = Int(cfg, key, value, cfg.AppealMaxEvidenceMb); break;
                case "risk.ai.enabled": cfg.AiEnabled = bool.TryParse(value, out var b) ? b : cfg.AiEnabled; break;
                case "risk.ai.min_score": cfg.AiMinScore = Int(cfg, key, value, cfg.AiMinScore); break;
                case "risk.vip.min_order_amount":
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) cfg.VipMinOrderAmount = d;
                    else cfg.ParseWarnings.Add($"{key}: giá trị '{value}' không phải số");
                    break;
            }
        }
        return cfg;
    }

    private static int Int(RiskConfig cfg, string key, string value, int fallback)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
        cfg.ParseWarnings.Add($"{key}: giá trị '{value}' không phải số nguyên, dùng mặc định {fallback}");
        return fallback;
    }
}