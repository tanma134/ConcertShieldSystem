using System.Net;
using System.Text.Json;
using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Services.Risks;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Services.Risks;

public sealed class RiskServiceException : Exception
{
    public int StatusCode { get; }
    public RiskServiceException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public interface IFraudAlertService
{
    Task<IngestRiskEventResponse> IngestAsync(IngestRiskEventRequest req, CancellationToken ct = default);
    Task<FraudAlertSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to);
    Task<PagedResult<FraudAlertListItemDto>> SearchAsync(FraudAlertQuery q);
    Task<FraudAlertDetailDto> GetDetailAsync(int id);
    Task<PagedResult<RiskBlockItemDto>> SearchBlocksAsync(RiskBlockQuery q);
    Task<RiskBlockResultDto> BlockAsync(BlockRequest req, int staffId);
    Task<RiskBlockResultDto> RestoreAsync(int decisionId, string reason, int staffId);
}

public sealed class FraudAlertService : IFraudAlertService
{
    private static readonly string[] ManualScopes = { "ACCOUNT", "TICKET" };
    private static readonly string[] ClosedStatuses = { "Restored", "Overturned" };
    private const int MaxManualBlockHours = 24 * 30;
    private const int MinReasonLength = 10;
    private static readonly TimeSpan AlertMergeWindow = TimeSpan.FromMinutes(15);

    private readonly AdminDbContext _db;
    private readonly IRiskConfigProvider _config;
    private readonly RiskScoreCalculator _calc;
    private readonly ILogger<FraudAlertService> _logger;

    private readonly IAiSummaryQueue _aiQueue;

    public FraudAlertService(AdminDbContext db, IRiskConfigProvider config, RiskScoreCalculator calc,
        IAiSummaryQueue aiQueue, ILogger<FraudAlertService> logger)
    {
        _db = db; _config = config; _calc = calc; _aiQueue = aiQueue; _logger = logger;
    }

    public async Task<IngestRiskEventResponse> IngestAsync(IngestRiskEventRequest req, CancellationToken ct = default)
    {
        var cfg = await _config.GetAsync(ct);
        var now = DateTime.UtcNow;

        if (req.UserId == null && string.IsNullOrWhiteSpace(req.SessionId)
            && string.IsNullOrWhiteSpace(req.DeviceFp) && string.IsNullOrWhiteSpace(req.IpAddress))
            throw new RiskServiceException(400, "At least one of UserId, SessionId, DeviceFp or IpAddress is required.");

        IPAddress? ip = null;
        if (!string.IsNullOrWhiteSpace(req.IpAddress) && !IPAddress.TryParse(req.IpAddress, out ip))
            throw new RiskServiceException(400, "IpAddress is invalid.");

        var ignored = new List<string>();
        var bot = ResolveRules(req.BotRules, "BOT", cfg, ignored);
        var fraud = ResolveRules(req.FraudRules, "FRAUD", cfg, ignored);
        var trust = ResolveRules(req.TrustRules, "TRUST", cfg, ignored);

        if (req.UserId is int uid)
        {
            if (cfg.Rules.TryGetValue("FRD_L02", out var l02) && l02.Enabled && fraud.All(r => r.Code != "FRD_L02")
                && await HadBlockAsync(uid, now.AddSeconds(-Math.Max(1, l02.WindowSec)), ct))
                fraud.Add(ToMatched("FRD_L02", l02, 1, "recent block"));

            if (cfg.Rules.TryGetValue("TRU_05", out var t05) && t05.Enabled && trust.All(r => r.Code != "TRU_05"))
            {
                var since = t05.WindowSec > 0 ? now.AddSeconds(-t05.WindowSec) : now.AddDays(-cfg.TrustRestoreDays);
                if (await _db.RiskDecisions.AsNoTracking().AnyAsync(d => d.UserId == uid && d.RestoredAt != null && d.RestoredAt >= since, ct))
                    trust.Add(ToMatched("TRU_05", t05, 1, "previously restored by staff"));
            }
        }

        var hardRule = await ResolveHardRuleAsync(req, ip, now, ct);

        var isVip = req.IsVipCustomer || (req.OrderAmount.HasValue && req.OrderAmount.Value >= cfg.VipMinOrderAmount);
        var botScore = _calc.CalculateScore(cfg, bot, trust, trustBlocked: hardRule != null);
        var fraudScore = _calc.CalculateScore(cfg, fraud, trust, trustBlocked: hardRule != null);

        var previousBlocks = await CountRecentBlocksAsync(cfg, req, ip, now, ct);
        var decision = _calc.Decide(cfg, botScore, fraudScore, new DecisionContext(hardRule, isVip, previousBlocks));

        var targets = new List<string>();
        if (decision.Action == RiskAction.Block)
        {
            targets = decision.Scopes.Where(s => ScopeHasTarget(s, req)).ToList();
            if (targets.Count == 0)
                decision = decision with
                {
                    Action = RiskAction.Hold,
                    BlockHours = null,
                    Scopes = Array.Empty<string>(),
                    ReasonCode = decision.ReasonCode + "_NO_TARGET"
                };
        }

        var response = new IngestRiskEventResponse
        {
            Level = decision.Level.ToString(),
            Action = decision.Action.ToString().ToUpperInvariant(),
            Dominant = decision.Dominant.ToString(),
            Score = decision.Score,
            BotScore = botScore.Score,
            FraudScore = fraudScore.Score,
            Scopes = targets,
            BlockHours = decision.BlockHours,
            ExpiresAt = decision.BlockHours is int h ? now.AddHours(h) : null,
            RequiresManualReview = decision.RequiresManualReview,
            ChallengeType = decision.ChallengeType,
            IsShadow = decision.IsShadow,
            ReasonCode = decision.ReasonCode,
            IgnoredRules = ignored
        };

        if (decision.Level == RiskLevel.Low || decision.HardRule == RiskScoreCalculator.HrWhitelist)
            return response;

        FraudAlert? alert = null;
        if (decision.Level >= RiskLevel.High)
            alert = await UpsertAlertAsync(req, decision, botScore, fraudScore, bot, fraud, now, ct);

        var scopesToStore = targets.Count > 0 ? targets : new List<string> { null! };
        var created = new List<RiskDecision>();
        foreach (var scope in scopesToStore)
        {
            var d = new RiskDecision
            {
                DecisionCode = NewDecisionCode(now),
                FraudAlert = alert,
                UserId = req.UserId,
                OrderId = req.OrderId,
                TicketId = req.TicketId,
                SessionId = req.SessionId,
                DeviceFp = req.DeviceFp,
                IpAddress = ip,
                BotScore = botScore.Score,
                FraudScore = fraudScore.Score,
                RiskLevel = decision.Level.ToString(),
                DominantType = decision.Dominant.ToString(),
                ScoreByGroup = JsonSerializer.Serialize(new { bot = botScore.ByGroup, fraud = fraudScore.ByGroup }),
                TriggeredRules = JsonSerializer.Serialize(
                    bot.Select(r => new { type = "BOT", code = r.Code, group = r.Group, score = r.Score, value = r.Value })
                       .Concat(fraud.Select(r => new { type = "FRAUD", code = r.Code, group = r.Group, score = r.Score, value = r.Value }))),
                TrustModifiers = JsonSerializer.Serialize(
                    trust.Select(r => new { code = r.Code, score = -Math.Abs(r.Score) })),
                HardRule = decision.HardRule,
                SignalsMissing = req.SignalsMissing.Count > 0 ? JsonSerializer.Serialize(req.SignalsMissing) : null,
                RuleVersion = cfg.RuleVersion,
                ReasonCode = decision.ReasonCode,
                Action = response.Action,
                Scope = scope,
                ReasonPublic = PublicReason(decision.Action),
                EscalationLevel = (short)decision.EscalationLevel,
                IsShadow = decision.IsShadow,
                Status = "Active",
                ExpiresAt = response.ExpiresAt,
                DecidedByType = "SYSTEM",
                CreatedAt = now
            };
            _db.RiskDecisions.Add(d);
            created.Add(d);
        }

        if (decision.Action == RiskAction.Block && !decision.IsShadow)
        {
            foreach (var byUser in created.Where(x => x.UserId.HasValue).GroupBy(x => x.UserId!.Value))
            {
                foreach (var row in byUser) row.NotifiedAt = now;
                _db.RiskNotifications.Add(RiskSupport.BlockNotice(byUser.First(), cfg, now));
            }
        }

        await _db.SaveChangesAsync(ct);

        response.FraudAlertId = alert?.FraudAlertId;

        if (alert != null && cfg.AiEnabled && decision.Score >= cfg.AiMinScore && alert.AiSummary == null)
            _aiQueue.TryEnqueue(alert.FraudAlertId);

        response.DecisionCodes = created.Select(d => d.DecisionCode).ToList();
        response.DecisionCode = response.DecisionCodes.FirstOrDefault();
        return response;
    }

    public async Task<FraudAlertSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to)
    {
        var cfg = await _config.GetAsync();
        var toUtc = to.HasValue ? ToUtc(to.Value) : DateTime.UtcNow;
        var fromUtc = from.HasValue ? ToUtc(from.Value) : toUtc.AddDays(-7);
        if (fromUtc > toUtc) throw new RiskServiceException(400, "From must be less than or equal to To.");

        var q = _db.FraudAlerts.AsNoTracking().Where(a => a.CreatedAt >= fromUtc && a.CreatedAt <= toUtc);

        var byLevel = await q.GroupBy(a => a.RiskLevel).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var byType = await q.GroupBy(a => a.AlertType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var byStatus = await q.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

        var slaCutoff = DateTime.UtcNow.AddHours(-cfg.AlertSlaHours);
        var openHigh = await q.CountAsync(a => (a.Status == "Open" || a.Status == "InReview")
                                                && (a.RiskLevel == "High" || a.RiskLevel == "Critical"));
        var overdue = await q.CountAsync(a => (a.Status == "Open" || a.Status == "InReview")
                                              && (a.RiskLevel == "High" || a.RiskLevel == "Critical")
                                              && a.CreatedAt < slaCutoff);

        static List<CountItemDto> Map(IEnumerable<(string Key, int Count)> src, string[] order)
        {
            int Rank(string key) { var i = Array.IndexOf(order, key); return i < 0 ? int.MaxValue : i; }
            return src.OrderBy(x => Rank(x.Key)).Select(x => new CountItemDto { Key = x.Key, Count = x.Count }).ToList();
        }

        return new FraudAlertSummaryDto
        {
            From = fromUtc,
            To = toUtc,
            Total = byLevel.Sum(x => x.Count),
            OpenHighOrCritical = openHigh,
            OverdueSla = overdue,
            ByLevel = Map(byLevel.Select(x => (x.Key, x.Count)), new[] { "Critical", "High", "Medium", "Low" }),
            ByType = Map(byType.Select(x => (x.Key, x.Count)), new[] { "Bot", "Fraud" }),
            ByStatus = Map(byStatus.Select(x => (x.Key, x.Count)), new[] { "Open", "InReview", "Resolved", "FalsePositive", "Dismissed" })
        };
    }

    public async Task<PagedResult<FraudAlertListItemDto>> SearchAsync(FraudAlertQuery qy)
    {
        var page = Math.Max(1, qy.Page);
        var size = Math.Clamp(qy.PageSize, 1, 100);

        var q = _db.FraudAlerts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(qy.RiskLevel)) q = q.Where(a => a.RiskLevel == qy.RiskLevel);
        if (!string.IsNullOrWhiteSpace(qy.AlertType)) q = q.Where(a => a.AlertType == qy.AlertType);
        if (!string.IsNullOrWhiteSpace(qy.Status)) q = q.Where(a => a.Status == qy.Status);
        if (qy.UserId.HasValue) q = q.Where(a => a.UserId == qy.UserId);
        if (qy.From.HasValue) { var f = ToUtc(qy.From.Value); q = q.Where(a => a.CreatedAt >= f); }
        if (qy.To.HasValue) { var t = ToUtc(qy.To.Value); q = q.Where(a => a.CreatedAt <= t); }

        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(a => a.CreatedAt)
                          .Skip((page - 1) * size).Take(size).ToListAsync();

        return new PagedResult<FraudAlertListItemDto>
        {
            Items = rows.Select(ToListItem).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    public async Task<FraudAlertDetailDto> GetDetailAsync(int id)
    {
        var alert = await _db.FraudAlerts.AsNoTracking().FirstOrDefaultAsync(a => a.FraudAlertId == id)
                    ?? throw new RiskServiceException(404, "Alert not found.");

        var decisions = await _db.RiskDecisions.AsNoTracking()
            .Where(d => d.FraudAlertId == id).OrderBy(d => d.CreatedAt).ToListAsync();

        return new FraudAlertDetailDto
        {
            Alert = ToListItem(alert),
            OrderId = alert.OrderId,
            TicketId = alert.TicketId,
            EventId = alert.EventId,
            SessionId = RiskSupport.MaskToken(alert.SessionId),
            Details = alert.Details,
            AiSummary = ParseJson(alert.AiSummary),
            Decisions = decisions.Select(d => new RiskDecisionDetailDto
            {
                RiskDecisionId = d.RiskDecisionId,
                DecisionCode = d.DecisionCode,
                RiskLevel = d.RiskLevel,
                Action = d.Action,
                Scope = d.Scope,
                Status = d.Status,
                IsShadow = d.IsShadow,
                BotScore = d.BotScore,
                FraudScore = d.FraudScore,
                DominantType = d.DominantType,
                HardRule = d.HardRule,
                RuleVersion = d.RuleVersion,
                EscalationLevel = d.EscalationLevel,
                ExpiresAt = d.ExpiresAt,
                DecidedByType = d.DecidedByType,
                DecidedBy = d.DecidedBy,
                ManualReason = d.ManualReason,
                RestoredBy = d.RestoredBy,
                RestoredAt = d.RestoredAt,
                RestoreReason = d.RestoreReason,
                ReasonCode = d.ReasonCode,
                SessionId = RiskSupport.MaskToken(d.SessionId),
                DeviceFp = RiskSupport.MaskToken(d.DeviceFp),
                IpAddress = RiskSupport.MaskIp(d.IpAddress?.ToString()),
                ScoreByGroup = ParseJson(d.ScoreByGroup),
                TriggeredRules = ParseJson(d.TriggeredRules),
                TrustModifiers = ParseJson(d.TrustModifiers),
                SignalsMissing = ParseJson(d.SignalsMissing),
                CreatedAt = d.CreatedAt
            }).ToList()
        };
    }

    public async Task<PagedResult<RiskBlockItemDto>> SearchBlocksAsync(RiskBlockQuery qy)
    {
        var page = Math.Max(1, qy.Page);
        var size = Math.Clamp(qy.PageSize, 1, 100);

        var q = _db.RiskDecisions.AsNoTracking().Where(d => d.Action == "BLOCK");
        if (!string.IsNullOrWhiteSpace(qy.Status)) q = q.Where(d => d.Status == qy.Status);
        if (!string.IsNullOrWhiteSpace(qy.Scope)) q = q.Where(d => d.Scope == qy.Scope);
        if (qy.UserId.HasValue) q = q.Where(d => d.UserId == qy.UserId);
        if (qy.FraudAlertId.HasValue) q = q.Where(d => d.FraudAlertId == qy.FraudAlertId);

        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync();

        return new PagedResult<RiskBlockItemDto>
        {
            Items = rows.Select(d => new RiskBlockItemDto
            {
                RiskDecisionId = d.RiskDecisionId,
                DecisionCode = d.DecisionCode,
                FraudAlertId = d.FraudAlertId,
                UserId = d.UserId,
                Scope = d.Scope,
                Status = d.Status,
                IsShadow = d.IsShadow,
                DecidedByType = d.DecidedByType,
                DecidedBy = d.DecidedBy,
                ExpiresAt = d.ExpiresAt,
                CreatedAt = d.CreatedAt
            }).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    public async Task<RiskBlockResultDto> BlockAsync(BlockRequest req, int staffId)
    {
        var cfg = await _config.GetAsync();
        var now = DateTime.UtcNow;

        var scope = (req.Scope ?? "").Trim().ToUpperInvariant();
        if (!ManualScopes.Contains(scope))
            throw new RiskServiceException(400, $"Manual block scope must be one of: {string.Join(", ", ManualScopes)}.");
        if (req.DurationHours < 1 || req.DurationHours > MaxManualBlockHours)
            throw new RiskServiceException(400, $"DurationHours must be between 1 and {MaxManualBlockHours}.");
        var reason = (req.Reason ?? "").Trim();
        if (reason.Length < MinReasonLength || reason.Length > 500)
            throw new RiskServiceException(400, $"A reason is required, {MinReasonLength}-500 characters long.");

        FraudAlert? alert = null;
        if (req.FraudAlertId.HasValue)
            alert = await _db.FraudAlerts.FirstOrDefaultAsync(a => a.FraudAlertId == req.FraudAlertId.Value)
                    ?? throw new RiskServiceException(404, "Alert not found.");

        var userId = req.UserId ?? alert?.UserId;
        var ticketId = req.TicketId ?? alert?.TicketId;
        var sessionId = string.IsNullOrWhiteSpace(req.SessionId) ? alert?.SessionId : req.SessionId;

        IPAddress? ip = null;
        if (!string.IsNullOrWhiteSpace(req.IpAddress) && !IPAddress.TryParse(req.IpAddress, out ip))
            throw new RiskServiceException(400, "IpAddress is invalid.");

        if (scope == "TICKET" && userId == null)
            throw new RiskServiceException(400, "Blocking a ticket requires the ticket owner's UserId (so the owner can be notified and can appeal).");

        var probe = new BlockRequest { UserId = userId, TicketId = ticketId, SessionId = sessionId, DeviceFp = req.DeviceFp, IpAddress = ip?.ToString() };
        if (!ScopeHasTarget(scope, probe.ToIngestLike()))
            throw new RiskServiceException(400, $"Scope {scope} requires the matching target ({ScopeTargetName(scope)}).");

        if (scope == "ACCOUNT" && userId == staffId)
            throw new RiskServiceException(400, "You cannot block your own account.");

        var (whitelisted, _) = await LookupListsAsync(userId, req.DeviceFp, ip?.ToString(), null, now, default);
        if (scope == "ACCOUNT" && whitelisted.Contains(("USER", userId.ToString()!)) ||
            scope == "DEVICE" && whitelisted.Contains(("DEVICE", req.DeviceFp ?? "")) ||
            scope == "IP" && whitelisted.Contains(("IP", ip?.ToString() ?? "")))
            throw new RiskServiceException(400, "The target is on the internal whitelist and cannot be blocked.");

        var activeBlocks = _db.RiskDecisions.AsNoTracking().Where(x =>
            x.Action == "BLOCK" && x.Status == "Active" && !x.IsShadow && x.Scope == scope
            && (x.ExpiresAt == null || x.ExpiresAt > now));
        var dup = scope switch
        {
            "ACCOUNT" => await activeBlocks.AnyAsync(x => x.UserId == userId),
            "TICKET" => await activeBlocks.AnyAsync(x => x.TicketId == ticketId),
            "SESSION" => await activeBlocks.AnyAsync(x => x.SessionId == sessionId),
            "DEVICE" => await activeBlocks.AnyAsync(x => x.DeviceFp == req.DeviceFp),
            _ => await activeBlocks.AnyAsync(x => x.IpAddress == ip)
        };
        if (dup) throw new RiskServiceException(409, "This target is already blocked; no additional block was created.");

        var d = new RiskDecision
        {
            DecisionCode = NewDecisionCode(now),
            FraudAlert = alert,
            UserId = userId,
            OrderId = alert?.OrderId,
            TicketId = ticketId,
            SessionId = sessionId,
            DeviceFp = req.DeviceFp,
            IpAddress = ip,
            BotScore = alert?.BotScore ?? 0,
            FraudScore = alert?.FraudScore ?? 0,
            RiskLevel = alert?.RiskLevel ?? "High",
            DominantType = alert?.AlertType,
            RuleVersion = cfg.RuleVersion,
            ReasonCode = "MANUAL_BLOCK",
            TriggeredRules = JsonSerializer.Serialize(new[]
            {
                new { type = "MANUAL", code = "MANUAL_BLOCK", group = "-", score = 0, value = reason }
            }),
            Action = "BLOCK",
            Scope = scope,
            ReasonPublic = PublicReason(RiskAction.Block),
            EscalationLevel = 1,
            IsShadow = false,
            Status = "Active",
            ExpiresAt = now.AddHours(req.DurationHours),
            DecidedByType = "STAFF",
            DecidedBy = staffId,
            ManualReason = reason,
            CreatedAt = now
        };
        _db.RiskDecisions.Add(d);

        if (userId.HasValue)
        {
            d.NotifiedAt = now;
            _db.RiskNotifications.Add(RiskSupport.BlockNotice(d, cfg, now));
        }

        if (alert != null && alert.Status is "Open" or "InReview"
            && !await RiskSupport.HasPendingAppealAsync(_db, alert.FraudAlertId))
        {
            alert.Status = "Resolved";
            alert.ReviewedBy = staffId;
            alert.ReviewedAt = now;
        }

        await SaveAsync();
        return ToResult(d);
    }

    public async Task<RiskBlockResultDto> RestoreAsync(int decisionId, string reason, int staffId)
    {
        var now = DateTime.UtcNow;
        reason = (reason ?? "").Trim();
        if (reason.Length < MinReasonLength || reason.Length > 500)
            throw new RiskServiceException(400, $"A reason is required, {MinReasonLength}-500 characters long.");

        var d = await _db.RiskDecisions.FirstOrDefaultAsync(x => x.RiskDecisionId == decisionId)
                ?? throw new RiskServiceException(404, "Decision not found.");

        if (d.Action != "BLOCK" && d.Action != "HOLD")
            throw new RiskServiceException(400, "Only Block/Hold decisions can be restored.");
        if (d.IsShadow)
            throw new RiskServiceException(400, "A shadow decision has not been enforced, so there is nothing to restore.");
        if (d.Status != "Active")
            throw new RiskServiceException(409, $"The decision is in status {d.Status} and cannot be restored.");

        d.Status = "Restored";
        d.RestoredBy = staffId;
        d.RestoredAt = now;
        d.RestoreReason = reason;

        if (d.UserId.HasValue)
            _db.RiskNotifications.Add(RiskSupport.RestoreNotice(d, now));

        if (d.FraudAlertId is int alertId && !await RiskSupport.HasPendingAppealAsync(_db, alertId))
        {
            var stillActive = await _db.RiskDecisions.AsNoTracking().AnyAsync(x =>
                x.FraudAlertId == alertId && x.RiskDecisionId != decisionId
                && x.Status == "Active" && !x.IsShadow && (x.Action == "BLOCK" || x.Action == "HOLD"));
            if (!stillActive)
            {
                var alert = await _db.FraudAlerts.FirstOrDefaultAsync(a => a.FraudAlertId == alertId);
                if (alert != null && alert.Status is "Open" or "InReview" or "Resolved")
                {
                    alert.Status = "FalsePositive";
                    alert.ReviewedBy = staffId;
                    alert.ReviewedAt = now;
                }
            }
        }

        await SaveAsync();
        return ToResult(d);
    }

    private async Task SaveAsync()
    {
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            throw new RiskServiceException(409, "The data was just updated by someone else. Please reload and try again.");
        }
    }

    private static List<MatchedRule> ResolveRules(IEnumerable<RuleHitDto>? hits, string expectedType,
        RiskConfig cfg, List<string> ignored)
    {
        var result = new List<MatchedRule>();
        foreach (var hit in hits ?? Enumerable.Empty<RuleHitDto>())
        {
            if (string.IsNullOrWhiteSpace(hit.Code) || !cfg.Rules.TryGetValue(hit.Code.Trim(), out var rule))
            { ignored.Add($"{hit.Code}: rule does not exist"); continue; }
            if (!rule.Enabled)
            { ignored.Add($"{hit.Code}: rule is disabled"); continue; }
            if (!string.Equals(rule.Type, expectedType, StringComparison.OrdinalIgnoreCase))
            { ignored.Add($"{hit.Code}: wrong type (expected {expectedType})"); continue; }
            result.Add(ToMatched(hit.Code.Trim().ToUpperInvariant(), rule, hit.Level, hit.Value));
        }
        return result;
    }

    private static MatchedRule ToMatched(string code, RiskRuleConfig rule, int level, string? value) =>
        new(code, rule.Group, rule.ScoreForLevel(level), value, rule.DisableTrust);

    private static bool ScopeHasTarget(string scope, IngestRiskEventRequest r) => scope switch
    {
        "ACCOUNT" => r.UserId.HasValue,
        "TICKET" => r.TicketId.HasValue,
        "SESSION" => !string.IsNullOrWhiteSpace(r.SessionId),
        "DEVICE" => !string.IsNullOrWhiteSpace(r.DeviceFp),
        "IP" => !string.IsNullOrWhiteSpace(r.IpAddress),
        _ => false
    };

    private static string ScopeTargetName(string scope) => scope switch
    {
        "ACCOUNT" => "UserId",
        "TICKET" => "TicketId",
        "SESSION" => "SessionId",
        "DEVICE" => "DeviceFp",
        _ => "IpAddress"
    };

    private static string PublicReason(RiskAction a) => a switch
    {
        RiskAction.Block => "Unusual activity was detected on your account.",
        RiskAction.Hold => "Your transaction is being reviewed further.",
        RiskAction.Challenge => "Additional verification is required to continue.",
        _ => ""
    };

    private static string NewDecisionCode(DateTime now) =>
        $"RD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static DateTime ToUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
    };

    private static JsonElement? ParseJson(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(s); } catch (JsonException) { return null; }
    }

    private static FraudAlertListItemDto ToListItem(FraudAlert a) => new()
    {
        FraudAlertId = a.FraudAlertId,
        UserId = a.UserId,
        AlertType = a.AlertType,
        RiskLevel = a.RiskLevel,
        RiskScore = a.RiskScore,
        BotScore = a.BotScore,
        FraudScore = a.FraudScore,
        Status = a.Status,
        CreatedAt = a.CreatedAt,
        ReviewedBy = a.ReviewedBy,
        ReviewedAt = a.ReviewedAt
    };

    private static RiskBlockResultDto ToResult(RiskDecision d) => new()
    {
        RiskDecisionId = d.RiskDecisionId,
        DecisionCode = d.DecisionCode,
        FraudAlertId = d.FraudAlertId,
        UserId = d.UserId,
        Scope = d.Scope,
        Status = d.Status,
        ExpiresAt = d.ExpiresAt
    };

    private Task<bool> HadBlockAsync(int userId, DateTime since, CancellationToken ct) =>
        _db.RiskDecisions.AsNoTracking().AnyAsync(d =>
            d.UserId == userId && d.Action == "BLOCK" && !d.IsShadow && d.CreatedAt >= since
            && d.Status != "Restored" && d.Status != "Overturned", ct);

    private Task<int> CountRecentBlocksAsync(RiskConfig cfg, IngestRiskEventRequest req, IPAddress? ip,
        DateTime now, CancellationToken ct)
    {
        var since = now.AddDays(-cfg.EscalationWindowDays);
        var uid = req.UserId; var dev = req.DeviceFp;
        return _db.RiskDecisions.AsNoTracking()
            .Where(d => d.Action == "BLOCK" && !d.IsShadow && d.DecidedByType == "SYSTEM" && d.CreatedAt >= since
                        && d.Status != "Restored" && d.Status != "Overturned"
                        && ((uid != null && d.UserId == uid) || (dev != null && d.DeviceFp == dev)
                            || (uid == null && ip != null && d.IpAddress == ip)))
            .Select(d => d.CreatedAt)
            .Distinct()
            .CountAsync(ct);
    }

    private async Task<(HashSet<(string, string)> White, HashSet<(string, string)> Black)> LookupListsAsync(
        int? userId, string? deviceFp, string? ip, string? cardHash, DateTime now, CancellationToken ct)
    {
        var values = new List<string>();
        if (userId.HasValue) values.Add(userId.Value.ToString());
        if (!string.IsNullOrWhiteSpace(deviceFp)) values.Add(deviceFp);
        if (!string.IsNullOrWhiteSpace(ip)) values.Add(ip);
        if (!string.IsNullOrWhiteSpace(cardHash)) values.Add(cardHash);

        var white = new HashSet<(string, string)>();
        var black = new HashSet<(string, string)>();
        if (values.Count == 0) return (white, black);

        var rows = await _db.RiskLists.AsNoTracking()
            .Where(l => (l.ExpiresAt == null || l.ExpiresAt > now) && values.Contains(l.ValueHash))
            .ToListAsync(ct);
        foreach (var l in rows)
            (l.ListType == "WHITELIST" ? white : black).Add((l.ValueType, l.ValueHash));
        return (white, black);
    }

    private async Task<string?> ResolveHardRuleAsync(IngestRiskEventRequest req, IPAddress? ip, DateTime now, CancellationToken ct)
    {
        var ipStr = ip?.ToString();
        var (white, black) = await LookupListsAsync(req.UserId, req.DeviceFp, ipStr, req.CardHash, now, ct);

        if ((req.UserId.HasValue && white.Contains(("USER", req.UserId.Value.ToString())))
            || (req.DeviceFp != null && white.Contains(("DEVICE", req.DeviceFp)))
            || (ipStr != null && white.Contains(("IP", ipStr))))
            return RiskScoreCalculator.HrWhitelist;

        if (req.CardHash != null && black.Contains(("CARD_HASH", req.CardHash))) return RiskScoreCalculator.HrBlacklistCard;
        if (req.DeviceFp != null && black.Contains(("DEVICE", req.DeviceFp))) return RiskScoreCalculator.HrBlacklistDevice;
        if (ipStr != null && black.Contains(("IP", ipStr))) return RiskScoreCalculator.HrBlacklistIp;
        return null;
    }

    private async Task<FraudAlert> UpsertAlertAsync(IngestRiskEventRequest req, RiskDecisionResult decision,
        ScoreResult botScore, ScoreResult fraudScore, List<MatchedRule> bot, List<MatchedRule> fraud,
        DateTime now, CancellationToken ct)
    {
        var type = decision.Dominant.ToString();
        var since = now - AlertMergeWindow;
        var uid = req.UserId; var sid = req.SessionId;

        FraudAlert? existing = null;
        if (uid != null || !string.IsNullOrWhiteSpace(sid))
        {
            existing = await _db.FraudAlerts.FirstOrDefaultAsync(a =>
                a.Status == "Open" && a.AlertType == type && a.CreatedAt >= since
                && (uid != null ? a.UserId == uid : a.SessionId == sid), ct);
        }

        var codes = string.Join(", ", (decision.Dominant == RiskType.Bot ? bot : fraud).Select(r => r.Code));
        var details = $"{type} score {decision.Score} ({decision.Level}); rules: {(codes.Length == 0 ? decision.HardRule ?? "-" : codes)}";
        if (details.Length > 2000) details = details[..2000];

        if (existing != null)
        {
            if (decision.Score >= existing.RiskScore)
            {
                existing.RiskScore = decision.Score;
                existing.BotScore = botScore.Score;
                existing.FraudScore = fraudScore.Score;
                existing.RiskLevel = decision.Level.ToString();
                existing.Details = details;
            }
            return existing;
        }

        var alert = new FraudAlert
        {
            UserId = req.UserId,
            OrderId = req.OrderId,
            TicketId = req.TicketId,
            EventId = req.EventId,
            AlertType = type,
            RiskScore = decision.Score,
            BotScore = botScore.Score,
            FraudScore = fraudScore.Score,
            RiskLevel = decision.Level.ToString(),
            SessionId = req.SessionId,
            Details = details,
            Status = "Open",
            CreatedAt = now
        };
        _db.FraudAlerts.Add(alert);
        return alert;
    }
}

internal static class BlockRequestExtensions
{
    public static IngestRiskEventRequest ToIngestLike(this BlockRequest r) => new()
    {
        UserId = r.UserId,
        TicketId = r.TicketId,
        SessionId = r.SessionId,
        DeviceFp = r.DeviceFp,
        IpAddress = r.IpAddress
    };
}