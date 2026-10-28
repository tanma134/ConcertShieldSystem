using AdminAPI.Data;
using AdminAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Services.Risks;


public sealed class FraudCaseService : IFraudCaseService
{
    private static readonly string[] OpenStatuses = { "Open", "InReview" };
    private const int MinNoteLength = 10;

    private readonly AdminDbContext _db;
    private readonly IRiskConfigProvider _config;

    public FraudCaseService(AdminDbContext db, IRiskConfigProvider config)
    {
        _db = db; _config = config;
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

        var q = _db.FraudAlerts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(qy.Status)) q = q.Where(a => a.Status == qy.Status);
        if (!string.IsNullOrWhiteSpace(qy.RiskLevel)) q = q.Where(a => a.RiskLevel == qy.RiskLevel);
        if (!string.IsNullOrWhiteSpace(qy.AlertType)) q = q.Where(a => a.AlertType == qy.AlertType);
        if (qy.UserId.HasValue) q = q.Where(a => a.UserId == qy.UserId);
        if (qy.AssignedTo.HasValue) q = q.Where(a => a.AssignedTo == qy.AssignedTo);
        if (qy.Unassigned == true) q = q.Where(a => a.AssignedTo == null && OpenStatuses.Contains(a.Status));
        if (qy.OverdueOnly == true)
            q = q.Where(a => OpenStatuses.Contains(a.Status)
                             && (a.RiskLevel == "High" || a.RiskLevel == "Critical") && a.CreatedAt < slaCutoff);
        if (qy.From.HasValue) { var f = Utc(qy.From.Value); q = q.Where(a => a.CreatedAt >= f); }
        if (qy.To.HasValue) { var t = Utc(qy.To.Value); q = q.Where(a => a.CreatedAt <= t); }
        if (!string.IsNullOrWhiteSpace(qy.Keyword))
        {
            if (!int.TryParse(qy.Keyword.Trim(), out var n))
                throw new RiskServiceException(400, "Keyword must be a numeric case, user, order or ticket id.");
            q = q.Where(a => a.FraudAlertId == n || a.UserId == n || a.OrderId == n || a.TicketId == n);
        }

        var total = await q.CountAsync();
        q = string.Equals(qy.SortBy, "riskScore", StringComparison.OrdinalIgnoreCase)
            ? (qy.Desc ? q.OrderByDescending(a => a.RiskScore).ThenByDescending(a => a.CreatedAt)
                       : q.OrderBy(a => a.RiskScore).ThenByDescending(a => a.CreatedAt))
            : (qy.Desc ? q.OrderByDescending(a => a.CreatedAt) : q.OrderBy(a => a.CreatedAt));
        var rows = await q.Skip((page - 1) * size).Take(size).ToListAsync();

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
                    IsOverdue = OpenStatuses.Contains(a.Status) && (a.RiskLevel == "High" || a.RiskLevel == "Critical") && due < now
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
        var a = await _db.FraudAlerts.FirstOrDefaultAsync(x => x.FraudAlertId == caseId)
                ?? throw new RiskServiceException(404, "Fraud case not found.");
        if (!OpenStatuses.Contains(a.Status))
            throw new RiskServiceException(409, $"The case is in status {a.Status} and can no longer be assigned.");

        var prevStatus = a.Status;
        int? prevAssignee = a.AssignedTo;
        if (prevAssignee == analystId)
            throw new RiskServiceException(409, "The case is already assigned to this analyst.");

        a.Status = "InReview";
        a.AssignedTo = analystId;
        a.AssignedAt = DateTime.UtcNow;
        await SaveAsync();

        return new FraudCaseResultDto
        {
            CaseId = a.FraudAlertId,
            Status = a.Status,
            AssignedTo = a.AssignedTo,
            AssignedAt = a.AssignedAt,
            ReviewedBy = a.ReviewedBy,
            ReviewedAt = a.ReviewedAt,
            PreviousStatus = prevStatus,
            PreviousAssignee = prevAssignee
        };
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

        var a = await _db.FraudAlerts.FirstOrDefaultAsync(x => x.FraudAlertId == caseId)
                ?? throw new RiskServiceException(404, "Fraud case not found.");
        if (!OpenStatuses.Contains(a.Status))
            throw new RiskServiceException(409, $"The case is already {a.Status}.");
        if (a.AssignedTo == null)
            throw new RiskServiceException(409, "The case must be assigned to an analyst before it can be resolved.");
        if (a.AssignedTo != actorId)
            throw new RiskServiceException(403, "Only the assigned analyst can resolve this case.");
        if (await RiskSupport.HasPendingAppealAsync(_db, caseId))
            throw new RiskServiceException(409, "The case has a pending appeal. Resolve the appeal first.");

        // FalsePositive/Dismissed nghĩa là "không có gian lận" -> không được còn biện pháp đang áp dụng lên user.
        if (status != "Resolved")
        {
            var nowUtc = DateTime.UtcNow;
            var stillEnforced = await _db.RiskDecisions.AsNoTracking().AnyAsync(d =>
                d.FraudAlertId == caseId && d.Status == "Active" && !d.IsShadow
                && (d.Action == "BLOCK" || d.Action == "HOLD")
                && (d.ExpiresAt == null || d.ExpiresAt > nowUtc));
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

        return new FraudCaseResultDto
        {
            CaseId = a.FraudAlertId,
            Status = a.Status,
            AssignedTo = a.AssignedTo,
            AssignedAt = a.AssignedAt,
            ReviewedBy = a.ReviewedBy,
            ReviewedAt = a.ReviewedAt,
            PreviousStatus = prevStatus,
            PreviousAssignee = prevAssignee
        };
    }

    private static string AppendNote(string? details, int actorId, string status, string note, DateTime at) =>
        (string.IsNullOrEmpty(details) ? "" : details + "\n") + $"[{at:yyyy-MM-dd HH:mm} UTC] {status} by #{actorId}: {note}";

    private static DateTime Utc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    private async Task SaveAsync()
    {
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            throw new RiskServiceException(409, "The data was just updated by someone else. Please reload and try again.");
        }
    }
}
