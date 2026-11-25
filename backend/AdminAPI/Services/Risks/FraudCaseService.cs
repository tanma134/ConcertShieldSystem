using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Services.Risks;


public sealed class FraudCaseService : IFraudCaseService
{
    private static readonly string[] OpenStatuses = { "Open", "InReview" };
    private const int MinNoteLength = 10;

    private readonly IFraudAlertRepository _alerts;
    private readonly IRiskDecisionRepository _decisions;
    private readonly IRiskAppealRepository _appeals;
    private readonly IRiskConfigProvider _config;
    private readonly IStaffDirectoryClient? _staff;

    public FraudCaseService(IFraudAlertRepository alerts, IRiskDecisionRepository decisions,
        IRiskAppealRepository appeals, IRiskConfigProvider config, IStaffDirectoryClient? staff = null)
    {
        _alerts = alerts; _decisions = decisions; _appeals = appeals; _config = config; _staff = staff;
    }

    public async Task<PagedResult<FraudCaseListItemDto>> SearchAsync(FraudCaseQuery qy)
    {
        var cfg = await _config.GetAsync();
        var page = Math.Max(1, qy.Page);
        var size = Math.Clamp(qy.PageSize, 1, 100);
        var now = DateTime.UtcNow;
        var slaCutoff = now.AddHours(-cfg.AlertSlaHours);

        if (qy.From.HasValue && qy.To.HasValue && Utc(qy.From.Value) > Utc(qy.To.Value))
            throw new RiskServiceException(400, "From must be less than or equal to To.");

        int? keywordId = null;
        if (!string.IsNullOrWhiteSpace(qy.Keyword))
        {
            if (!int.TryParse(qy.Keyword.Trim(), out var n))
                throw new RiskServiceException(400, "Keyword must be a numeric case, user, order or ticket id.");
            keywordId = n;
        }

        var (rows, total) = await _alerts.SearchCasesAsync(qy, slaCutoff, keywordId, page, size);

        return new PagedResult<FraudCaseListItemDto>
        {
            Items = rows.Select(a =>
            {
                var due = a.CreatedAt.AddHours(cfg.AlertSlaHours);
                return new FraudCaseListItemDto
                {
                    CaseId = a.FraudAlertId,
                    UserId = a.UserId,
                    AlertType = a.AlertType,
                    RiskLevel = a.RiskLevel,
                    RiskScore = a.RiskScore,
                    Status = a.Status,
                    AssignedTo = a.AssignedTo,
                    AssignedAt = a.AssignedAt,
                    CreatedAt = a.CreatedAt,
                    ReviewedAt = a.ReviewedAt,
                    SlaDueAt = due,
                    IsOverdue = OpenStatuses.Contains(a.Status) && (a.RiskLevel == "High" || a.RiskLevel == "Critical") && due < now,
                    Recommendation = a.Recommendation,
                    RecommendedOutcome = a.RecommendedOutcome,
                    RecommendedBy = a.RecommendedBy,
                    RecommendedAt = a.RecommendedAt
                };
            }).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    public async Task<FraudCaseResultDto> AssignAsync(int caseId, int analystId, int actorId)
    {
        if (analystId <= 0) throw new RiskServiceException(400, "AnalystId is invalid.");
        var a = await _alerts.GetForUpdateAsync(caseId)
                ?? throw new RiskServiceException(404, "Fraud case not found.");
        if (!OpenStatuses.Contains(a.Status))
            throw new RiskServiceException(409, $"The case is in status {a.Status} and can no longer be assigned.");

        if (_staff != null && !await _staff.IsActiveStaffAsync(analystId))
            throw new RiskServiceException(400, "The assignee must be an active Staff member.");

        var prevStatus = a.Status;
        int? prevAssignee = a.AssignedTo;
        if (prevAssignee == analystId)
            throw new RiskServiceException(409, "The case is already assigned to this staff member.");

        a.Status = "InReview";
        a.AssignedTo = analystId;
        a.AssignedAt = DateTime.UtcNow;
        // A new assignee starts a fresh review; never keep the previous reviewer's proposal.
        a.Recommendation = null;
        a.RecommendedOutcome = null;
        a.RecommendedBy = null;
        a.RecommendedAt = null;
        await SaveAsync();

        return Result(a, prevStatus, prevAssignee);
    }

    public async Task<FraudCaseResultDto> ResolveAsync(int caseId, string outcome, string note, int actorId)
    {
        note = (note ?? "").Trim();
        if (note.Length < MinNoteLength || note.Length > 500)
            throw new RiskServiceException(400, $"A resolution note is required, {MinNoteLength}-500 characters long.");

        var status = (outcome ?? "").Trim().ToLowerInvariant() switch
        {
            "confirmed" => "Resolved",
            "falsepositive" => "FalsePositive",
            "dismissed" => "Dismissed",
            _ => throw new RiskServiceException(400, "Outcome must be Confirmed, FalsePositive or Dismissed.")
        };

        var a = await _alerts.GetForUpdateAsync(caseId)
                ?? throw new RiskServiceException(404, "Fraud case not found.");
        if (!OpenStatuses.Contains(a.Status))
            throw new RiskServiceException(409, $"The case is already {a.Status}.");
        if (await _appeals.HasPendingForAlertAsync(caseId))
            throw new RiskServiceException(409, "The case has a pending appeal. Resolve the appeal first.");

        if (status != "Resolved")
        {
            var nowUtc = DateTime.UtcNow;
            var stillEnforced = await _decisions.HasActiveRestrictionForAlertAsync(caseId, nowUtc);
            if (stillEnforced)
                throw new RiskServiceException(409,
                    $"Cannot close as {status} while a Block/Hold decision is still active. Restore it first.");
        }

        var prevStatus = a.Status;
        var prevAssignee = a.AssignedTo;
        a.Status = status;
        a.ReviewedBy = actorId;
        a.ReviewedAt = DateTime.UtcNow;
        a.Details = AppendNote(a.Details, actorId, status, note, a.ReviewedAt.Value);
        await SaveAsync();

        return Result(a, prevStatus, prevAssignee);
    }

    public async Task<FraudCaseResultDto> RecommendAsync(int caseId, string outcome, string note, int actorId)
    {
        note = (note ?? "").Trim();
        if (note.Length < MinNoteLength || note.Length > 1000)
            throw new RiskServiceException(400, $"A recommendation note is required, {MinNoteLength}-1000 characters long.");

        var recommended = (outcome ?? "").Trim().ToLowerInvariant() switch
        {
            "confirmed" => "Confirmed",
            "falsepositive" => "FalsePositive",
            "dismissed" => "Dismissed",
            _ => throw new RiskServiceException(400, "Outcome must be Confirmed, FalsePositive or Dismissed.")
        };

        var a = await _alerts.GetForUpdateAsync(caseId)
                ?? throw new RiskServiceException(404, "Fraud case not found.");

        if (a.Status != "InReview")
            throw new RiskServiceException(409, "The case must be InReview before it can be reviewed.");
        if (a.AssignedTo != actorId)
            throw new RiskServiceException(403, "Only the assigned Staff member can submit a recommendation.");

        a.Recommendation = note;
        a.RecommendedOutcome = recommended;
        a.RecommendedBy = actorId;
        a.RecommendedAt = DateTime.UtcNow;
        a.Details = AppendNote(a.Details, actorId, $"REVIEW {recommended}", note, a.RecommendedAt.Value);
        await SaveAsync();

        return Result(a, a.Status, a.AssignedTo);
    }

    private static FraudCaseResultDto Result(FraudAlert a, string? prevStatus, int? prevAssignee) => new()
    {
        CaseId = a.FraudAlertId,
        Status = a.Status,
        AssignedTo = a.AssignedTo,
        AssignedAt = a.AssignedAt,
        ReviewedBy = a.ReviewedBy,
        ReviewedAt = a.ReviewedAt,
        PreviousStatus = prevStatus,
        PreviousAssignee = prevAssignee,
        Recommendation = a.Recommendation,
        RecommendedOutcome = a.RecommendedOutcome,
        RecommendedBy = a.RecommendedBy,
        RecommendedAt = a.RecommendedAt
    };

    private static string AppendNote(string? details, int actorId, string status, string note, DateTime at) =>
        (string.IsNullOrEmpty(details) ? "" : details + "\n") + $"[{at:yyyy-MM-dd HH:mm} UTC] {status} by #{actorId}: {note}";

    private static DateTime Utc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    private async Task SaveAsync()
    {
        try { await _alerts.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            throw new RiskServiceException(409, "The data was just updated by someone else. Please reload and try again.");
        }
    }
}
