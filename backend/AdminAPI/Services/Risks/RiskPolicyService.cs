using System.Text.RegularExpressions;
using AdminAPI.Data;
using AdminAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Services.Risks;


public sealed class RiskPolicyService : IRiskPolicyService
{
    private static readonly string[] Categories = { "risk_config", "risk_rule" };
    private static readonly string[] Modes = { "SHADOW", "ENFORCE" };
    private const int MinReasonLength = 10;
    private const string VersionKey = "risk.rule_version";
    private const string RulePrefix = "risk.rule.";

    private readonly AdminDbContext _db;
    private readonly IRiskConfigProvider _config;

    public RiskPolicyService(AdminDbContext db, IRiskConfigProvider config)
    {
        _db = db; _config = config;
    }

    public async Task<List<RiskPolicyItemDto>> GetAsync(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category) && !Categories.Contains(category))
            throw new RiskServiceException(400, "Category must be risk_config or risk_rule.");
        var q = _db.SystemParameters.AsNoTracking().Where(p => Categories.Contains(p.Category));
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(p => p.Category == category);
        return await q.OrderBy(p => p.Category).ThenBy(p => p.ParamKey)
            .Select(p => new RiskPolicyItemDto
            {
                Key = p.ParamKey,
                Value = p.ParamValue,
                Category = p.Category,
                Description = p.Description,
                UpdatedBy = p.UpdatedBy,
                UpdatedAt = p.UpdatedAt
            }).ToListAsync();
    }

    public async Task<UpdateRiskPolicyResult> UpdateAsync(UpdateRiskPolicyRequest req, int adminId)
    {
        var reason = (req.Reason ?? "").Trim();
        if (reason.Length < MinReasonLength || reason.Length > 500)
            throw new RiskServiceException(400, $"A reason is required, {MinReasonLength}-500 characters long.");
        if (req.Items == null || req.Items.Count == 0)
            throw new RiskServiceException(400, "At least one parameter is required.");

        var dup = req.Items.GroupBy(i => (i.Key ?? "").Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new RiskServiceException(400, $"Key {dup.Key} appears more than once.");

        var keys = req.Items.Select(i => (i.Key ?? "").Trim()).ToList();
        var rows = await _db.SystemParameters.Where(p => Categories.Contains(p.Category)).ToListAsync();
        var byKey = rows.ToDictionary(r => r.ParamKey, StringComparer.OrdinalIgnoreCase);

        var changes = new List<RiskPolicyChangeDto>();
        foreach (var item in req.Items)
        {
            var key = (item.Key ?? "").Trim();
            var value = (item.Value ?? "").Trim();
            if (!byKey.TryGetValue(key, out var row))
                throw new RiskServiceException(404, $"Parameter {key} does not exist.");
            if (value.Length == 0 || value.Length > 500)
                throw new RiskServiceException(400, $"{key}: value must be 1-500 characters.");
            if (row.ParamValue == value) continue;
            changes.Add(new RiskPolicyChangeDto { Key = row.ParamKey, OldValue = row.ParamValue, NewValue = value });
        }
        if (changes.Count == 0)
            throw new RiskServiceException(409, "No parameter value has changed.");

        // Validate cấu hình sau khi áp thay đổi (kết hợp giữa các tham số) trước khi ghi DB.
        var merged = rows.ToDictionary(r => r.ParamKey, r => r.ParamValue, StringComparer.OrdinalIgnoreCase);
        foreach (var c in changes) merged[c.Key] = c.NewValue;
        var changedRuleCodes = changes
            .Where(c => c.Key.StartsWith(RulePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Key[RulePrefix.Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Validate(RiskConfig.Parse(merged), changedRuleCodes);

        var now = DateTime.UtcNow;

        // Mỗi lần đổi chính sách -> tăng rule_version để mọi risk_decision về sau truy vết được cấu hình đã dùng.
        if (!changes.Any(c => string.Equals(c.Key, VersionKey, StringComparison.OrdinalIgnoreCase))
            && byKey.TryGetValue(VersionKey, out var versionRow))
        {
            changes.Add(new RiskPolicyChangeDto
            {
                Key = versionRow.ParamKey,
                OldValue = versionRow.ParamValue,
                NewValue = NextVersion(versionRow.ParamValue, now)
            });
        }
        foreach (var c in changes)
        {
            var row = byKey[c.Key];
            row.ParamValue = c.NewValue;
            row.UpdatedBy = adminId;
            row.UpdatedAt = now;
        }

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            throw new RiskServiceException(409, "The data was just updated by someone else. Please reload and try again.");
        }

        _config.Invalidate();
        return new UpdateRiskPolicyResult { Changes = changes };
    }

    private static string NextVersion(string current, DateTime now)
    {
        var m = Regex.Match(current ?? "", @"^v(\d+)\.(\d+)$");
        return m.Success ? $"v{m.Groups[1].Value}.{int.Parse(m.Groups[2].Value) + 1}" : $"v{now:yyyyMMddHHmm}";
    }

    private static void Validate(RiskConfig c, HashSet<string> changedRuleCodes)
    {
        if (c.ParseWarnings.Count > 0)
            throw new RiskServiceException(400, "Invalid configuration: " + string.Join("; ", c.ParseWarnings));
        if (!Modes.Contains(c.Mode))
            throw new RiskServiceException(400, "risk.mode must be SHADOW or ENFORCE.");
        if (!(0 < c.ThresholdMedium && c.ThresholdMedium < c.ThresholdHigh && c.ThresholdHigh < c.ThresholdCritical && c.ThresholdCritical <= 100))
            throw new RiskServiceException(400, "Thresholds must satisfy 0 < medium < high < critical <= 100.");
        if (c.CapGroup is < 1 or > 100 || c.CapTrust is < 0 or > 100 || c.BonusCombo is < 0 or > 100)
            throw new RiskServiceException(400, "Score caps and combo bonus must be between 0 and 100.");
        if (c.ComboMinGroups < 2 || c.CriticalMinGroups < 1)
            throw new RiskServiceException(400, "Minimum group counts are out of range.");
        if (c.BlockHoursLevel1 < 1 || c.BlockHoursLevel2 < c.BlockHoursLevel1 || c.BlockHoursLevel2 > 24 * 30)
            throw new RiskServiceException(400, "Block hours must satisfy 1 <= level1 <= level2 <= 720.");
        if (c.EscalationWindowDays < 1 || c.TrustRestoreDays < 1 || c.AlertSlaHours < 1
            || c.AppealDeadlineDays < 1 || c.AppealSlaHours < 1)
            throw new RiskServiceException(400, "Day and hour parameters must be positive.");
        if (c.AppealMaxEvidenceFiles is < 0 or > 20 || c.AppealMaxEvidenceMb is < 1 or > 50)
            throw new RiskServiceException(400, "Evidence limits are out of range.");
        if (c.AiMinScore is < 0 or > 100)
            throw new RiskServiceException(400, "risk.ai.min_score must be between 0 and 100.");
        if (c.VipMinOrderAmount < 0)
            throw new RiskServiceException(400, "risk.vip.min_order_amount must not be negative.");

        foreach (var (code, r) in c.Rules)
        {
            // Chỉ kiểm tra rule đang được sửa; dữ liệu seed cũ không được chặn các chỉnh sửa không liên quan.
            if (!changedRuleCodes.Contains(code)) continue;

            var isTrust = string.Equals(r.Type, "Trust", StringComparison.OrdinalIgnoreCase);
            if (!isTrust
                && !string.Equals(r.Type, "Bot", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(r.Type, "Fraud", StringComparison.OrdinalIgnoreCase))
                throw new RiskServiceException(400, $"Rule {code}: type must be Bot, Fraud or Trust.");

            // Rule Trust dùng để TRỪ điểm (calculator lấy giá trị tuyệt đối) nên được lưu âm hoặc dương.
            var scoreOk = isTrust
                ? Math.Abs(r.Score) <= 100 && Math.Abs(r.ScoreL2 ?? 0) <= 100
                : r.Score is >= 0 and <= 100 && (r.ScoreL2 is null or (>= 0 and <= 100));
            if (!scoreOk)
                throw new RiskServiceException(400,
                    isTrust ? $"Rule {code}: score magnitude must be at most 100."
                            : $"Rule {code}: score must be between 0 and 100.");
            if (r.WindowSec < 0)
                throw new RiskServiceException(400, $"Rule {code}: window_sec must not be negative.");
            if (r.Threshold is < 0 || r.ThresholdL2 is < 0)
                throw new RiskServiceException(400, $"Rule {code}: threshold must not be negative.");
            if (r.HasLevel2 && r.ThresholdL2 <= r.Threshold)
                throw new RiskServiceException(400, $"Rule {code}: threshold_l2 must be greater than threshold.");
        }
    }
}
