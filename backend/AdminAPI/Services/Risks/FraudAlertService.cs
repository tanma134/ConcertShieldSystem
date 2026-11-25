using System.Net;
using System.Text.Json;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Repositories;
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
    private static readonly string[] ManualScopes = { "ACCOUNT", "TICKET", "IP" };
    private const int MaxManualBlockHours = 24 * 30;
    private const int MaxActiveScopesPerUser = 2;
    private const int MinReasonLength = 10;
    private static readonly TimeSpan AlertMergeWindow = TimeSpan.FromMinutes(15);
    private static readonly string[] CredentialActions = { "PASSWORD_CHANGED", "PASSWORD_RESET" };

    private readonly IFraudAlertRepository _alerts;
    private readonly IRiskDecisionRepository _decisions;
    private readonly IRiskListRepository _lists;
    private readonly IRiskNotificationRepository _notifications;
    private readonly IAuditLogRepository _audit;
    private readonly IRiskAppealRepository _appeals;
    private readonly IRiskConfigProvider _config;
    private readonly RiskScoreCalculator _calc;
    private readonly ILogger<FraudAlertService> _logger;
    private readonly ITicketRiskClient? _tickets;
    private readonly IStaffDirectoryClient? _accounts;

    private readonly IAiSummaryQueue _aiQueue;

    public FraudAlertService(IFraudAlertRepository alerts, IRiskDecisionRepository decisions,
        IRiskListRepository lists, IRiskNotificationRepository notifications, IAuditLogRepository audit,
        IRiskAppealRepository appeals, IRiskConfigProvider config, RiskScoreCalculator calc,
        IAiSummaryQueue aiQueue, ILogger<FraudAlertService> logger,
        ITicketRiskClient? tickets = null, IStaffDirectoryClient? accounts = null)
    {
        _alerts = alerts;
        _decisions = decisions;
        _lists = lists;
        _notifications = notifications;
        _audit = audit;
        _appeals = appeals;
        _config = config;
        _calc = calc;
        _aiQueue = aiQueue;
        _logger = logger;
        _tickets = tickets;
        _accounts = accounts;
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
                && await _decisions.HasRecentBlockAsync(uid, now.AddSeconds(-Math.Max(1, l02.WindowSec)), ct))
                fraud.Add(ToMatched("FRD_L02", l02, 1, "recent block"));

            // FRD_A03: password reset/change followed by ticket hold or order creation (booking events include EventId).
            if (req.EventId != null && cfg.Rules.TryGetValue("FRD_A03", out var a03) && a03.Enabled
                && fraud.All(r => r.Code != "FRD_A03"))
            {
                var credSince = now.AddSeconds(-Math.Max(1, a03.WindowSec));
                if (await _audit.AnyAsync(uid, CredentialActions, credSince, ct))
                    fraud.Add(ToMatched("FRD_A03", a03, 1, "password changed shortly before booking"));
            }

            if (cfg.Rules.TryGetValue("TRU_05", out var t05) && t05.Enabled && trust.All(r => r.Code != "TRU_05"))
            {
                var since = t05.WindowSec > 0 ? now.AddSeconds(-t05.WindowSec) : now.AddDays(-cfg.TrustRestoreDays);
                if (await _decisions.AnyRestoredSinceAsync(uid, since, ct))
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
            await _decisions.AddAsync(d, ct);
            created.Add(d);
        }

        if (decision.Action == RiskAction.Block && !decision.IsShadow)
        {
            foreach (var byUser in created.Where(x => x.UserId.HasValue).GroupBy(x => x.UserId!.Value))
            {
                foreach (var row in byUser) row.NotifiedAt = now;
                await _notifications.AddAsync(RiskSupport.BlockNotice(byUser.First(), cfg, now), ct);
            }
        }

        await _decisions.SaveChangesAsync(ct);

        if (decision.Action == RiskAction.Block && !decision.IsShadow && _accounts != null)
        {
            foreach (var blockedUserId in created
                         .Where(x => x.Scope == "ACCOUNT" && x.UserId.HasValue)
                         .Select(x => x.UserId!.Value)
                         .Distinct())
                await _accounts.RevokeSessionsAsync(blockedUserId, CancellationToken.None);
        }

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

        var slaCutoff = DateTime.UtcNow.AddHours(-cfg.AlertSlaHours);
        var metrics = await _alerts.GetMetricsAsync(fromUtc, toUtc, slaCutoff);

        static List<CountItemDto> Map(IEnumerable<(string Key, int Count)> src, string[] order)
        {
            int Rank(string key) { var i = Array.IndexOf(order, key); return i < 0 ? int.MaxValue : i; }
            return src.OrderBy(x => Rank(x.Key)).Select(x => new CountItemDto { Key = x.Key, Count = x.Count }).ToList();
        }

        return new FraudAlertSummaryDto
        {
            From = fromUtc,
            To = toUtc,
            Total = metrics.Total,
            OpenHighOrCritical = metrics.OpenHighOrCritical,
            OverdueSla = metrics.OverdueSla,
            ByLevel = Map(metrics.ByLevel, new[] { "Critical", "High", "Medium", "Low" }),
            ByType = Map(metrics.ByType, new[] { "Bot", "Fraud" }),
            ByStatus = Map(metrics.ByStatus, new[] { "Open", "InReview", "Resolved", "FalsePositive", "Dismissed" })
        };
    }

    public async Task<PagedResult<FraudAlertListItemDto>> SearchAsync(FraudAlertQuery qy)
    {
        var page = Math.Max(1, qy.Page);
        var size = Math.Clamp(qy.PageSize, 1, 100);

        var (rows, total) = await _alerts.SearchAsync(qy,
            qy.From.HasValue ? ToUtc(qy.From.Value) : null,
            qy.To.HasValue ? ToUtc(qy.To.Value) : null,
            page, size);

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
        var alert = await _alerts.GetByIdAsync(id)
                    ?? throw new RiskServiceException(404, "Alert not found.");

        var decisions = await _decisions.GetByAlertAsync(id);

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

        var (rows, total) = await _decisions.SearchBlocksAsync(qy, page, size);

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
                IpAddress = RiskSupport.MaskIp(d.IpAddress?.ToString()),
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
            alert = await _alerts.GetForUpdateAsync(req.FraudAlertId.Value)
                    ?? throw new RiskServiceException(404, "Alert not found.");

        var userId = req.UserId ?? alert?.UserId;
        var ticketId = req.TicketId ?? alert?.TicketId;
        var sessionId = string.IsNullOrWhiteSpace(req.SessionId) ? alert?.SessionId : req.SessionId;

        IPAddress? ip = null;
        if (!string.IsNullOrWhiteSpace(req.IpAddress) && !IPAddress.TryParse(req.IpAddress, out ip))
            throw new RiskServiceException(400, "IpAddress is invalid.");

        if ((scope == "TICKET" || scope == "IP") && userId == null)
            throw new RiskServiceException(400, "Blocking a ticket or an IP requires the owner's UserId (so the owner can be notified and can appeal).");

        var probe = new BlockRequest { UserId = userId, TicketId = ticketId, SessionId = sessionId, DeviceFp = req.DeviceFp, IpAddress = ip?.ToString() };
        if (!ScopeHasTarget(scope, probe.ToIngestLike()))
            throw new RiskServiceException(400, $"Scope {scope} requires the matching target ({ScopeTargetName(scope)}).");

        AccountInfo? target = null;
        if (userId.HasValue && scope is "ACCOUNT" or "TICKET" or "IP")
        {
            if (_accounts == null)
                throw new RiskServiceException(503,
                    "Account verification is not configured, so a block cannot be created safely.");
            target = await _accounts.FindAccountAsync(userId.Value)
                     ?? throw new RiskServiceException(404, $"User #{userId} does not exist.");
            if (!target.IsActive)
                throw new RiskServiceException(409, $"User #{userId} is not an active account.");
        }

        // Blocking any Staff (including yourself) would cut off the operations team.
        if (scope == "ACCOUNT" && userId == staffId)
            throw new RiskServiceException(400, "You cannot block your own account.");
        if (scope == "ACCOUNT" && target?.Roles.Any(r =>
                string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, "Staff", StringComparison.OrdinalIgnoreCase)) == true)
            throw new RiskServiceException(403,
                "Administrator and Staff accounts cannot be blocked from the fraud console. Use the user management screen instead.");

        // A TICKET block revokes someone's property, so the ticket must exist and
        // belong to the user named on the block.
        if (scope == "TICKET")
        {
            if (_tickets == null)
                throw new RiskServiceException(503,
                    "Ticket verification is not configured, so a ticket block cannot be created safely.");

            var ticket = await _tickets.FindTicketAsync(ticketId!.Value)
                         ?? throw new RiskServiceException(404, $"Ticket #{ticketId} does not exist.");
            if (ticket.OwnerUserId != userId)
                throw new RiskServiceException(400,
                    $"Ticket #{ticketId} belongs to user #{ticket.OwnerUserId}, not user #{userId}.");
            if (ticket.CheckedInAt.HasValue)
                throw new RiskServiceException(409,
                    $"Ticket #{ticketId} has already been checked in and cannot be blocked.");
            if (string.Equals(ticket.Status, "Returned", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ticket.Status, "Revoked", StringComparison.OrdinalIgnoreCase))
                throw new RiskServiceException(409,
                    $"Ticket #{ticketId} is already {ticket.Status} and cannot be blocked.");
            if (string.Equals(ticket.Status, "Blocked", StringComparison.OrdinalIgnoreCase))
                throw new RiskServiceException(409, $"Ticket #{ticketId} is already blocked.");
        }

        var (whitelisted, _) = await _lists.LookupAsync(LookupValues(userId, req.DeviceFp, ip?.ToString(), null), now);
        var targetValues = new List<(string Type, string Value)>();
        if (userId.HasValue && scope is "ACCOUNT" or "TICKET" or "IP")
            targetValues.Add(("USER", userId.Value.ToString()));
        if (scope == "DEVICE" && !string.IsNullOrWhiteSpace(req.DeviceFp))
            targetValues.Add(("DEVICE", req.DeviceFp));
        if (scope == "IP" && ip != null)
            targetValues.Add(("IP", ip.ToString()));
        if (targetValues.Any(tv => whitelisted.Contains(tv)))
            throw new RiskServiceException(400, "The target is on the internal whitelist and cannot be blocked.");

        var dup = await _decisions.HasActiveBlockAsync(scope, userId, ticketId, sessionId, req.DeviceFp, ip, now);
        if (dup) throw new RiskServiceException(409, "This target is already blocked; no additional block was created.");
        if (userId.HasValue)
        {
            var activeScopes = await _decisions.GetActiveBlockScopesAsync(userId.Value, scope, now);

            if (activeScopes.Count >= MaxActiveScopesPerUser)
                throw new RiskServiceException(409,
                    $"User #{userId} already has {activeScopes.Count} active block scopes " +
                    $"({string.Join(", ", activeScopes)}). Restore one of them before adding another.");
        }

        var ticketBlocked = false;
        if (scope == "TICKET" && _tickets != null)
        {
            await _tickets.BlockTicketAsync(ticketId!.Value, userId!.Value, reason, staffId,
                now.AddHours(req.DurationHours));
            ticketBlocked = true;
        }

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
        await _decisions.AddAsync(d);

        if (userId.HasValue)
        {
            d.NotifiedAt = now;
            await _notifications.AddAsync(RiskSupport.BlockNotice(d, cfg, now));
        }

        if (alert != null && alert.Status is "Open" or "InReview"
            && !await _appeals.HasPendingForAlertAsync(alert.FraudAlertId))
        {
            alert.Status = "Resolved";
            alert.ReviewedBy = staffId;
            alert.ReviewedAt = now;
        }

        try
        {
            await SaveAsync();
        }
        catch
        {
            if (ticketBlocked && _tickets != null)
            {
                try { await _tickets.UnblockTicketAsync(ticketId!.Value, "Risk decision could not be saved", staffId); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not roll back ticket block {TicketId}", ticketId); }
            }
            throw;
        }
        if (scope == "ACCOUNT" && userId.HasValue && _accounts != null)
            await _accounts.RevokeSessionsAsync(userId.Value);

        return ToResult(d);
    }

    public async Task<RiskBlockResultDto> RestoreAsync(int decisionId, string reason, int staffId)
    {
        var now = DateTime.UtcNow;
        reason = (reason ?? "").Trim();
        if (reason.Length < MinReasonLength || reason.Length > 500)
            throw new RiskServiceException(400, $"A reason is required, {MinReasonLength}-500 characters long.");

        var d = await _decisions.GetByIdAsync(decisionId, tracked: true)
                ?? throw new RiskServiceException(404, "Decision not found.");

        if (d.Action != "BLOCK" && d.Action != "HOLD")
            throw new RiskServiceException(400, "Only Block/Hold decisions can be restored.");
        if (d.IsShadow)
            throw new RiskServiceException(400, "A shadow decision has not been enforced, so there is nothing to restore.");
        if (d.Status != "Active")
            throw new RiskServiceException(409, $"The decision is in status {d.Status} and cannot be restored.");

        if (string.Equals(d.Scope, "TICKET", StringComparison.OrdinalIgnoreCase)
            && d.TicketId.HasValue && _tickets != null)
        {
            // Do the ticket side first: if TicketAPI fails, the decision must stay Active
            // instead of showing "Restored" while the ticket is still blocked.
            try
            {
                await _tickets.UnblockTicketAsync(d.TicketId.Value,
                    $"Block restored by admin: {reason}", staffId);
            }
            catch (Exception ex) when (ex is not RiskServiceException)
            {
                _logger.LogError(ex, "Could not unblock ticket {TicketId} while restoring decision {Id}",
                    d.TicketId, decisionId);
                throw new RiskServiceException(502,
                    "TicketAPI could not unblock the ticket. The block was not restored; please retry.");
            }
        }

        d.Status = "Restored";
        d.RestoredBy = staffId;
        d.RestoredAt = now;
        d.RestoreReason = reason;

        if (d.UserId.HasValue)
            await _notifications.AddAsync(RiskSupport.RestoreNotice(d, now));

        if (d.FraudAlertId is int alertId && !await _appeals.HasPendingForAlertAsync(alertId))
        {
            var stillActive = await _decisions.HasActiveRestrictionForAlertExceptAsync(alertId,
                new[] { decisionId });
            if (!stillActive)
            {
                var alert = await _alerts.GetForUpdateAsync(alertId);
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
        try { await _decisions.SaveChangesAsync(); }
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
        ReviewedAt = a.ReviewedAt,
        AssignedTo = a.AssignedTo,
        AssignedAt = a.AssignedAt
    };

    private static RiskBlockResultDto ToResult(RiskDecision d) => new()
    {
        RiskDecisionId = d.RiskDecisionId,
        DecisionCode = d.DecisionCode,
        FraudAlertId = d.FraudAlertId,
        UserId = d.UserId,
        Scope = d.Scope,
        Status = d.Status,
        ExpiresAt = d.ExpiresAt,
        IpAddress = RiskSupport.MaskIp(d.IpAddress?.ToString())
    };

    private Task<int> CountRecentBlocksAsync(RiskConfig cfg, IngestRiskEventRequest req, IPAddress? ip,
        DateTime now, CancellationToken ct) =>
        _decisions.CountRecentBlocksAsync(req.UserId, req.DeviceFp, ip,
            now.AddDays(-cfg.EscalationWindowDays), ct);

    private static List<string> LookupValues(int? userId, string? deviceFp, string? ip, string? cardHash)
    {
        var values = new List<string>();
        if (userId.HasValue) values.Add(userId.Value.ToString());
        if (!string.IsNullOrWhiteSpace(deviceFp)) values.Add(deviceFp);
        if (!string.IsNullOrWhiteSpace(ip)) values.Add(ip);
        if (!string.IsNullOrWhiteSpace(cardHash)) values.Add(cardHash);
        return values;
    }

    private async Task<string?> ResolveHardRuleAsync(IngestRiskEventRequest req, IPAddress? ip, DateTime now, CancellationToken ct)
    {
        var ipStr = ip?.ToString();
        var (white, black) = await _lists.LookupAsync(LookupValues(req.UserId, req.DeviceFp, ipStr, req.CardHash), now, ct);

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

        FraudAlert? existing = null;
        if (req.UserId != null || !string.IsNullOrWhiteSpace(req.SessionId))
            existing = await _alerts.FindOpenAlertAsync(type, req.UserId, req.SessionId, since, ct);

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
        await _alerts.AddAsync(alert, ct);
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
