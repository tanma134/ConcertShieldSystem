using System.Text.Json;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Services.Risks;


public sealed class AppealService : IAppealService
{
    private const int MinReasonLength = 10;

    private readonly IRiskAppealRepository _appeals;
    private readonly IRiskDecisionRepository _decisions;
    private readonly IFraudAlertRepository _alerts;
    private readonly IRiskNotificationRepository _notifications;
    private readonly IRiskConfigProvider _config;
    private readonly ITicketRiskClient? _tickets;
    private readonly ILogger<AppealService>? _logger;

    public AppealService(IRiskAppealRepository appeals, IRiskDecisionRepository decisions,
    IFraudAlertRepository alerts, IRiskNotificationRepository notifications,
    IRiskConfigProvider config, ITicketRiskClient? tickets = null, ILogger<AppealService>? logger = null)
    {
        _appeals = appeals; _decisions = decisions; _alerts = alerts; _notifications = notifications;
        _config = config; _tickets = tickets; _logger = logger;
    }

    public async Task<RiskDecisionPublicDto> GetPublicDecisionAsync(int decisionId, int requestingUserId, CancellationToken ct = default)
    {
        var cfg = await _config.GetAsync(ct);
        var d = await _decisions.GetByIdAsync(decisionId, ct: ct)
        ?? throw new AppealServiceException(404, "Decision not found.");


        if (d.UserId != requestingUserId || d.IsShadow)
            throw new AppealServiceException(404, "Decision not found.");

        var existing = await _appeals.GetByDecisionAsync(decisionId, ct);

        var (canAppeal, cannotReason) = EvaluateAppealEligibility(cfg, d, existing);

        return ToPublicDto(cfg, d, canAppeal, cannotReason, existing);
    }

    // Danh sách decision (BLOCK/HOLD, không phải shadow) của chính user, 30 ngày gần nhất - hiển thị ở My Profile.
    public async Task<List<RiskDecisionPublicDto>> GetMyDecisionsAsync(int userId, CancellationToken ct = default)
    {
        var cfg = await _config.GetAsync(ct);
        var since = DateTime.UtcNow.AddDays(-30);

        var decisions = await _decisions.GetRecentForUserAsync(userId, since, 20, ct);

        var ids = decisions.Select(x => x.RiskDecisionId).ToList();
        var appeals = await _appeals.GetByDecisionIdsAsync(ids, ct);
        var byDecision = appeals
        .GroupBy(a => a.RiskDecisionId)
        .ToDictionary(g => g.Key, g => g.First());

        return decisions.Select(d =>
        {
            byDecision.TryGetValue(d.RiskDecisionId, out var existing);
            var (canAppeal, reason) = EvaluateAppealEligibility(cfg, d, existing);
            return ToPublicDto(cfg, d, canAppeal, reason, existing);
        }).ToList();
    }

    private static RiskDecisionPublicDto ToPublicDto(RiskConfig cfg, RiskDecision d, bool canAppeal, string? cannotReason, RiskAppeal? existing) => new()
    {
        RiskDecisionId = d.RiskDecisionId,
        DecisionCode = d.DecisionCode,
        Action = d.Action,
        Scope = d.Scope,
        Status = d.Status,
        ReasonPublic = d.ReasonPublic ?? "Unusual activity was detected on your account.",
        ExpiresAt = d.ExpiresAt,
        CreatedAt = d.CreatedAt,
        CanAppeal = canAppeal,
        CannotAppealReason = cannotReason,
        AppealDeadlineAt = cfg.AppealDueAt(d.Scope, d.NotifiedAt ?? d.CreatedAt),
        TicketId = d.TicketId,
        ExistingAppealId = existing?.RiskAppealId,
        ExistingAppealStatus = existing?.Status
    };

    public async Task<RiskDecisionAdminDto> GetAdminDecisionAsync(int decisionId, CancellationToken ct = default)
    {
        var d = await _decisions.GetByIdAsync(decisionId, ct: ct)
        ?? throw new AppealServiceException(404, "Decision not found.");
        return ToAdminDto(d);
    }

    public async Task<AppealResultDto> SubmitAppealAsync(SubmitAppealRequest req, int userId, CancellationToken ct = default)
    {
        var cfg = await _config.GetAsync(ct);
        var now = DateTime.UtcNow;

        var reason = (req.Reason ?? "").Trim();
        if (reason.Length < MinReasonLength || reason.Length > 2000)
            throw new AppealServiceException(400, $"An appeal reason is required, {MinReasonLength}-2000 characters long.");

        if (req.Evidence.Count > cfg.AppealMaxEvidenceFiles)
            throw new AppealServiceException(400, $"At most {cfg.AppealMaxEvidenceFiles} files can be attached.");

        var maxBytes = (long)cfg.AppealMaxEvidenceMb * 1024 * 1024;
        foreach (var f in req.Evidence)
        {
            if (string.IsNullOrWhiteSpace(f.Url)
            || !Uri.TryCreate(f.Url, UriKind.Absolute, out var evidenceUri)
            || (evidenceUri.Scheme != Uri.UriSchemeHttps && evidenceUri.Scheme != Uri.UriSchemeHttp))
                throw new AppealServiceException(400, "Each evidence file needs a valid http(s) URL.");
            if (string.IsNullOrWhiteSpace(f.FileName) || f.SizeBytes < 0)
                throw new AppealServiceException(400, "The evidence file information is invalid.");
            if (f.SizeBytes > maxBytes)
                throw new AppealServiceException(400, $"File '{f.FileName}' exceeds {cfg.AppealMaxEvidenceMb}MB.");
        }

        var d = await _decisions.GetByIdAsync(req.RiskDecisionId, ct: ct)
        ?? throw new AppealServiceException(404, "Decision not found.");

        if (d.UserId != userId)
            throw new AppealServiceException(404, "Decision not found.");

        var existing = await _appeals.GetByDecisionAsync(req.RiskDecisionId, ct);
        var (canAppeal, cannotReason) = EvaluateAppealEligibility(cfg, d, existing);
        if (!canAppeal)
            throw new AppealServiceException(409, cannotReason ?? "An appeal cannot be submitted for this decision.");

        var appeal = new RiskAppeal
        {
            RiskDecisionId = req.RiskDecisionId,
            UserId = userId,
            Reason = reason,
            Evidence = req.Evidence.Count > 0 ? JsonSerializer.Serialize(req.Evidence) : null,
            Status = "Pending",
            SlaDueAt = now.AddHours(cfg.AppealSlaHours),
            CreatedAt = now
        };

        await _appeals.AddAsync(appeal, ct);

        try
        {
            await _appeals.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {

            var alreadyExists = await _appeals.AnyForDecisionAsync(req.RiskDecisionId, appeal.RiskAppealId, ct);
            if (alreadyExists)
                throw new AppealServiceException(409, "This decision already has an appeal.");
            throw;
        }

        return ToResult(appeal);
    }


    public async Task<PagedResult<AppealListItemDto>> SearchAppealsAsync(AppealQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var now = DateTime.UtcNow;

        var (rows, total) = await _appeals.SearchAsync(query, page, size, now, ct);

        return new PagedResult<AppealListItemDto>
        {
            Items = rows.Select(x => new AppealListItemDto
            {
                RiskAppealId = x.Appeal.RiskAppealId,
                RiskDecisionId = x.Appeal.RiskDecisionId,
                DecisionCode = x.DecisionCode,
                UserId = x.Appeal.UserId,
                Status = x.Appeal.Status,
                SlaDueAt = x.Appeal.SlaDueAt,
                IsOverdue = x.Appeal.Status == "Pending" && x.Appeal.SlaDueAt < now,
                CreatedAt = x.Appeal.CreatedAt
            }).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    public async Task<AppealDetailDto> GetAppealDetailAsync(int appealId, CancellationToken ct = default)
    {
        var a = await _appeals.GetByIdAsync(appealId, ct: ct)
        ?? throw new AppealServiceException(404, "Appeal not found.");
        var d = await _decisions.GetByIdAsync(a.RiskDecisionId, ct: ct)
        ?? throw new AppealServiceException(404, "Related decision not found.");

        return new AppealDetailDto
        {
            RiskAppealId = a.RiskAppealId,
            Reason = a.Reason,
            Evidence = ParseEvidence(a.Evidence),
            Status = a.Status,
            SlaDueAt = a.SlaDueAt,
            IsOverdue = a.Status == "Pending" && a.SlaDueAt < DateTime.UtcNow,
            ReviewedBy = a.ReviewedBy,
            ReviewedAt = a.ReviewedAt,
            ReviewNote = a.ReviewNote,
            AiSummary = ParseJson(a.AiSummary),
            CreatedAt = a.CreatedAt,
            Decision = ToAdminDto(d)
        };
    }

    public async Task<AppealResultDto> ResolveAppealAsync(int appealId, ResolveAppealRequest req, int staffId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var decisionWord = (req.Decision ?? "").Trim();
        if (decisionWord != "Accept" && decisionWord != "Reject")
            throw new AppealServiceException(400, "Decision must be 'Accept' or 'Reject'.");

        var note = (req.ReviewNote ?? "").Trim();
        if (note.Length < MinReasonLength || note.Length > 1000)
            throw new AppealServiceException(400, $"A review reason is required, {MinReasonLength}-1000 characters long.");

        var a = await _appeals.GetByIdAsync(appealId, tracked: true, ct)
        ?? throw new AppealServiceException(404, "Appeal not found.");

        if (a.Status != "Pending")
            throw new AppealServiceException(409, $"This appeal has already been resolved ({a.Status}).");

        var d = await _decisions.GetByIdAsync(a.RiskDecisionId, tracked: true, ct)
        ?? throw new AppealServiceException(404, "Related decision not found.");
        var selfReview = d.DecidedByType == "STAFF" && d.DecidedBy == staffId;

        var accepted = decisionWord == "Accept";
        a.Status = accepted ? "Accepted" : "Rejected";
        a.ReviewedBy = staffId;
        a.ReviewedAt = now;
        a.ReviewNote = selfReview ? $"[SELF-REVIEW] {note}" : note;

        var lifted = new List<int>();
        if (accepted)
        {

            var decisionId = d.RiskDecisionId;
            var relatedAlertId = d.FraudAlertId;
         
            var related = await _decisions.GetForAppealResolutionAsync(decisionId, relatedAlertId, ct);
            var toLift = related.Where(x => x.Status == "Active" && !x.IsShadow).ToList();
            var ticketIdsToUnblock = toLift
            .Where(x => string.Equals(x.Scope, "TICKET", StringComparison.OrdinalIgnoreCase) && x.TicketId.HasValue)
            .Select(x => x.TicketId!.Value)
            .Distinct()
            .ToList();

            if (ticketIdsToUnblock.Count > 0)
            {
                if (_tickets == null)
                    throw new AppealServiceException(503,
                    "TicketAPI is not configured, so the ticket cannot be unblocked. Please contact an administrator.");
                foreach (var ticketId in ticketIdsToUnblock)
                {
                    try { await _tickets.UnblockTicketAsync(ticketId, $"Appeal #{a.RiskAppealId} accepted", staffId, ct); }
                    catch (RiskServiceException ex)
                    {
                        throw new AppealServiceException(ex.StatusCode, ex.Message);
                    }
                }
            }

            foreach (var x in toLift)
            {
                x.Status = "Overturned";
                x.RestoredBy = staffId;
                x.RestoredAt = now;
                x.RestoreReason = $"Appeal #{a.RiskAppealId} accepted: {note}";
                lifted.Add(x.RiskDecisionId);
            }


            if (d.FraudAlertId is int alertId)
            {
                var stillActive = await _decisions.HasActiveRestrictionForAlertExceptAsync(alertId, lifted, ct);
                if (!stillActive && !await _appeals.HasPendingForAlertAsync(alertId, a.RiskAppealId, ct))
                {
                    var alert = await _alerts.GetForUpdateAsync(alertId, ct);
                    if (alert != null && alert.Status is "Open" or "InReview" or "Resolved")
                    {
                        alert.Status = "FalsePositive";
                        alert.ReviewedBy = staffId;
                        alert.ReviewedAt = now;
                    }
                }
            }
        }

        TicketRiskRevokeResult? revoke = null;
        if (!accepted && string.Equals(d.Scope, "TICKET", StringComparison.OrdinalIgnoreCase)
        && d.TicketId.HasValue && d.UserId.HasValue && _tickets != null)
        {
            // ticket_return_requests.reason is varchar(500); the staff note can be 1000 chars.
            var revokeReason = $"Appeal #{a.RiskAppealId} rejected: {note}";
            if (revokeReason.Length > 500) revokeReason = revokeReason[..500];
            revoke = await _tickets.RevokeAndRefundAsync(d.TicketId.Value, d.UserId.Value,
            a.RiskAppealId, staffId, revokeReason, ct);
        }

        await _notifications.AddAsync(RiskSupport.AppealNotice(a, d, accepted, now), ct);

        try
        {
            await _appeals.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppealServiceException(409, "This appeal was just resolved by someone else. Please reload.");
        }

        var result = ToResult(a);
        result.LiftedDecisionIds = lifted;
        if (revoke != null)
        {
            result.TicketRevoked = revoke.TicketRevoked;
            result.RefundStatus = revoke.RefundStatus;
            result.RefundAmount = revoke.RefundAmount;
            result.RefundReference = revoke.RefundReference;
            result.RefundError = revoke.RefundError;
        }
        return result;
    }

    private static (bool CanAppeal, string? Reason) EvaluateAppealEligibility(RiskConfig cfg, RiskDecision d, RiskAppeal? existing)
    {
        if (existing != null)
            return (false, $"This decision already has an appeal (status: {existing.Status}).");
        if (d.Action != "BLOCK")
            return (false, "Only Block decisions can be appealed.");
        if (d.IsShadow)
            return (false, "This decision has not been enforced (shadow mode), so no appeal is needed.");
        if (d.Status != "Active")
            return (false, $"The decision is in status {d.Status} and cannot be appealed.");
        var now = DateTime.UtcNow;
        if (d.Action == "BLOCK" && d.ExpiresAt.HasValue && d.ExpiresAt.Value <= now)
            return (false, "This block has expired and is no longer in effect.");
        var deadline = cfg.AppealDueAt(d.Scope, d.NotifiedAt ?? d.CreatedAt);
        if (now > deadline)
            return (false, $"The {cfg.AppealWindowAdjective(d.Scope)} deadline to submit an appeal has passed.");
        return (true, null);
    }

    private static List<EvidenceFileDto> ParseEvidence(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<EvidenceFileDto>();
        try { return JsonSerializer.Deserialize<List<EvidenceFileDto>>(json) ?? new(); }
        catch (JsonException) { return new List<EvidenceFileDto>(); }
    }

    private static JsonElement? ParseJson(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(s); } catch (JsonException) { return null; }
    }

    private static RiskDecisionAdminDto ToAdminDto(RiskDecision d) => new()
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
        ReasonCode = d.ReasonCode,
        EscalationLevel = d.EscalationLevel,
        ExpiresAt = d.ExpiresAt,
        DecidedByType = d.DecidedByType,
        DecidedBy = d.DecidedBy,
        ManualReason = d.ManualReason,
        RestoredBy = d.RestoredBy,
        RestoredAt = d.RestoredAt,
        RestoreReason = d.RestoreReason,
        SessionId = RiskSupport.MaskToken(d.SessionId),
        DeviceFp = RiskSupport.MaskToken(d.DeviceFp),
        IpAddress = RiskSupport.MaskIp(d.IpAddress?.ToString()),
        ScoreByGroup = ParseJson(d.ScoreByGroup),
        TriggeredRules = ParseJson(d.TriggeredRules),
        TrustModifiers = ParseJson(d.TrustModifiers),
        SignalsMissing = ParseJson(d.SignalsMissing),
        CreatedAt = d.CreatedAt,
        UserId = d.UserId,
        OrderId = d.OrderId,
        TicketId = d.TicketId,
        FraudAlertId = d.FraudAlertId
    };

    private static AppealResultDto ToResult(RiskAppeal a) => new()
    {
        RiskAppealId = a.RiskAppealId,
        RiskDecisionId = a.RiskDecisionId,
        Status = a.Status,
        SlaDueAt = a.SlaDueAt,
        CreatedAt = a.CreatedAt
    };
}

public sealed class AppealServiceException : Exception
{
    public int StatusCode { get; }
    public AppealServiceException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}
