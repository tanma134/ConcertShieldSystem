using System.Text.Json;
using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;

using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Services.Risks;


public sealed class AppealService : IAppealService
{
    private const int MinReasonLength = 10;

    private readonly AdminDbContext _db;
    private readonly IRiskConfigProvider _config;

    public AppealService(AdminDbContext db, IRiskConfigProvider config)
    {
        _db = db; _config = config;
    }

    public async Task<RiskDecisionPublicDto> GetPublicDecisionAsync(int decisionId, int requestingUserId, CancellationToken ct = default)
    {
        var cfg = await _config.GetAsync(ct);
        var d = await _db.RiskDecisions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RiskDecisionId == decisionId, ct)
            ?? throw new AppealServiceException(404, "Decision not found.");


        if (d.UserId != requestingUserId || d.IsShadow)
            throw new AppealServiceException(404, "Decision not found.");

        var existing = await _db.RiskAppeals.AsNoTracking()
            .FirstOrDefaultAsync(a => a.RiskDecisionId == decisionId, ct);

        var (canAppeal, cannotReason) = EvaluateAppealEligibility(cfg, d, existing);

        return ToPublicDto(d, canAppeal, cannotReason, existing);
    }

    // Danh sách decision (BLOCK/HOLD, không phải shadow) của chính user, 30 ngày gần nhất - hiển thị ở My Profile.
    public async Task<List<RiskDecisionPublicDto>> GetMyDecisionsAsync(int userId, CancellationToken ct = default)
    {
        var cfg = await _config.GetAsync(ct);
        var since = DateTime.UtcNow.AddDays(-30);

        var decisions = await _db.RiskDecisions.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsShadow
                        && (x.Action == "BLOCK" || x.Action == "HOLD")
                        && x.CreatedAt >= since)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        var ids = decisions.Select(x => x.RiskDecisionId).ToList();
        var appeals = await _db.RiskAppeals.AsNoTracking()
            .Where(a => ids.Contains(a.RiskDecisionId))
            .ToListAsync(ct);
        var byDecision = appeals
            .GroupBy(a => a.RiskDecisionId)
            .ToDictionary(g => g.Key, g => g.First());

        return decisions.Select(d =>
        {
            byDecision.TryGetValue(d.RiskDecisionId, out var existing);
            var (canAppeal, reason) = EvaluateAppealEligibility(cfg, d, existing);
            return ToPublicDto(d, canAppeal, reason, existing);
        }).ToList();
    }

    private static RiskDecisionPublicDto ToPublicDto(RiskDecision d, bool canAppeal, string? cannotReason, RiskAppeal? existing) => new()
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
        ExistingAppealId = existing?.RiskAppealId,
        ExistingAppealStatus = existing?.Status
    };

    public async Task<RiskDecisionAdminDto> GetAdminDecisionAsync(int decisionId, CancellationToken ct = default)
    {
        var d = await _db.RiskDecisions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.RiskDecisionId == decisionId, ct)
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

        var d = await _db.RiskDecisions.FirstOrDefaultAsync(x => x.RiskDecisionId == req.RiskDecisionId, ct)
                 ?? throw new AppealServiceException(404, "Decision not found.");

        if (d.UserId != userId)
            throw new AppealServiceException(404, "Decision not found.");

        var existing = await _db.RiskAppeals.FirstOrDefaultAsync(a => a.RiskDecisionId == req.RiskDecisionId, ct);
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

        _db.RiskAppeals.Add(appeal);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {

            var alreadyExists = await _db.RiskAppeals.AsNoTracking()
                .AnyAsync(a => a.RiskDecisionId == req.RiskDecisionId && a.RiskAppealId != appeal.RiskAppealId, ct);
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

        var q = _db.RiskAppeals.AsNoTracking()
            .Join(_db.RiskDecisions.AsNoTracking(), a => a.RiskDecisionId, d => d.RiskDecisionId,
                  (a, d) => new { Appeal = a, d.DecisionCode });

        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(x => x.Appeal.Status == query.Status);
        if (query.UserId.HasValue) q = q.Where(x => x.Appeal.UserId == query.UserId);
        if (query.OverdueOnly == true) q = q.Where(x => x.Appeal.Status == "Pending" && x.Appeal.SlaDueAt < now);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.Appeal.Status == "Pending" ? 0 : 1)
                          .ThenBy(x => x.Appeal.SlaDueAt)
                          .Skip((page - 1) * size).Take(size).ToListAsync(ct);

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
        var a = await _db.RiskAppeals.AsNoTracking().FirstOrDefaultAsync(x => x.RiskAppealId == appealId, ct)
                ?? throw new AppealServiceException(404, "Appeal not found.");
        var d = await _db.RiskDecisions.AsNoTracking().FirstOrDefaultAsync(x => x.RiskDecisionId == a.RiskDecisionId, ct)
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

        var a = await _db.RiskAppeals.FirstOrDefaultAsync(x => x.RiskAppealId == appealId, ct)
                ?? throw new AppealServiceException(404, "Appeal not found.");

        if (a.Status != "Pending")
            throw new AppealServiceException(409, $"This appeal has already been resolved ({a.Status}).");

        var d = await _db.RiskDecisions.FirstOrDefaultAsync(x => x.RiskDecisionId == a.RiskDecisionId, ct)
                ?? throw new AppealServiceException(404, "Related decision not found.");


        if (d.DecidedByType == "STAFF" && d.DecidedBy == staffId)
            throw new AppealServiceException(400, "You cannot review an appeal for a decision you made yourself.");

        var accepted = decisionWord == "Accept";
        a.Status = accepted ? "Accepted" : "Rejected";
        a.ReviewedBy = staffId;
        a.ReviewedAt = now;
        a.ReviewNote = note;

        var lifted = new List<int>();
        if (accepted)
        {

            var decisionId = d.RiskDecisionId;
            var createdAt = d.CreatedAt;
            var action = d.Action;
            var ownerId = d.UserId;
            var sessionId = d.SessionId;
            var related = await _db.RiskDecisions.Where(x =>
                    x.RiskDecisionId == decisionId
                    || (x.CreatedAt == createdAt && x.Action == action && x.Status == "Active" && !x.IsShadow
                        && ((ownerId != null && x.UserId == ownerId)
                            || (sessionId != null && x.SessionId == sessionId))))
                .ToListAsync(ct);

            foreach (var x in related.Where(x => x.Status == "Active" && !x.IsShadow))
            {
                x.Status = "Overturned";
                x.RestoredBy = staffId;
                x.RestoredAt = now;
                x.RestoreReason = $"Appeal #{a.RiskAppealId} accepted: {note}";
                lifted.Add(x.RiskDecisionId);
            }


            if (d.FraudAlertId is int alertId)
            {
                var stillActive = await _db.RiskDecisions.AsNoTracking().AnyAsync(x =>
                    x.FraudAlertId == alertId && !lifted.Contains(x.RiskDecisionId)
                    && x.Status == "Active" && !x.IsShadow && (x.Action == "BLOCK" || x.Action == "HOLD"), ct);
                if (!stillActive && !await RiskSupport.HasPendingAppealAsync(_db, alertId, a.RiskAppealId, ct))
                {
                    var alert = await _db.FraudAlerts.FirstOrDefaultAsync(x => x.FraudAlertId == alertId, ct);
                    if (alert != null && alert.Status is "Open" or "InReview" or "Resolved")
                    {
                        alert.Status = "FalsePositive";
                        alert.ReviewedBy = staffId;
                        alert.ReviewedAt = now;
                    }
                }
            }
        }

        _db.RiskNotifications.Add(RiskSupport.AppealNotice(a, d, accepted, now));

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppealServiceException(409, "This appeal was just resolved by someone else. Please reload.");
        }

        var result = ToResult(a);
        result.LiftedDecisionIds = lifted;
        return result;
    }

    private static (bool CanAppeal, string? Reason) EvaluateAppealEligibility(RiskConfig cfg, RiskDecision d, RiskAppeal? existing)
    {
        if (existing != null)
            return (false, $"This decision already has an appeal (status: {existing.Status}).");
        if (d.Action != "BLOCK" && d.Action != "HOLD")
            return (false, "Only Block or Hold decisions can be appealed.");
        if (d.IsShadow)
            return (false, "This decision has not been enforced (shadow mode), so no appeal is needed.");
        if (d.Status != "Active")
            return (false, $"The decision is in status {d.Status} and cannot be appealed.");
        var now = DateTime.UtcNow;
        if (d.Action == "BLOCK" && d.ExpiresAt.HasValue && d.ExpiresAt.Value <= now)
            return (false, "This block has expired and is no longer in effect.");
        var deadline = (d.NotifiedAt ?? d.CreatedAt).AddDays(cfg.AppealDeadlineDays);
        if (now > deadline)
            return (false, $"The {cfg.AppealDeadlineDays}-day deadline to submit an appeal has passed.");
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
        SessionId = RiskSupport.MaskToken(d.SessionId),   // BR-238
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