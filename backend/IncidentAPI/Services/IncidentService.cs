using IncidentAPI.Data;
using IncidentAPI.DTOs;
using IncidentAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace IncidentAPI.Services;

// Triển khai toàn bộ nghiệp vụ Incident Management cho 5 Use Case (UC_16.1 - UC_16.5)
public class IncidentService : IIncidentService
{
    private readonly IncidentDbContext _db;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(IncidentDbContext db, ILogger<IncidentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // UC_16.1: Staff báo cáo sự cố mới tại hiện trường
    public async Task<IncidentDetailDto> CreateIncidentAsync(
        CreateIncidentRequest req, int reporterId, string reporterName, CancellationToken ct = default)
    {
        if (req.EventId <= 0)
            throw new IncidentServiceException(400, "Invalid EventId.");

        if (string.IsNullOrWhiteSpace(req.Title))
            throw new IncidentServiceException(400, "Incident title is required.");

        if (string.IsNullOrWhiteSpace(req.Location))
            throw new IncidentServiceException(400, "On-site location is required.");

        if (string.IsNullOrWhiteSpace(req.Description))
            throw new IncidentServiceException(400, "Incident description is required.");

        var now = DateTime.UtcNow;

        var incident = new Incident
        {
            EventId = req.EventId,
            EventName = req.EventName,
            ReporterId = reporterId,
            ReporterName = string.IsNullOrWhiteSpace(reporterName) ? "Staff" : reporterName,
            Title = req.Title.Trim(),
            Category = string.IsNullOrWhiteSpace(req.Category) ? "Security" : req.Category.Trim(),
            Severity = string.IsNullOrWhiteSpace(req.Severity) ? "Medium" : req.Severity.Trim(),
            Location = req.Location.Trim(),
            Description = req.Description.Trim(),
            Status = "Reported",
            IsEscalated = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync(ct);

        // Ghi nhật ký ban đầu (UC_16.3)
        var initialUpdate = new IncidentUpdate
        {
            IncidentId = incident.Id,
            UpdatedById = reporterId,
            UpdatedByName = incident.ReporterName,
            ActionType = "Reported",
            Note = $"Initial incident report: {incident.Title}",
            PreviousStatus = null,
            NewStatus = "Reported",
            CreatedAt = now
        };

        _db.IncidentUpdates.Add(initialUpdate);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(incident.Id, reporterId, true, ct);
    }

    // Lấy chi tiết sự cố kèm bằng chứng và nhật ký
    public async Task<IncidentDetailDto> GetDetailAsync(
        int id, int requestingUserId, bool isAdmin, CancellationToken ct = default)
    {
        var incident = await _db.Incidents
            .AsNoTracking()
            .Include(i => i.Evidences)
            .Include(i => i.Updates)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (incident == null)
            throw new IncidentServiceException(404, $"Incident #{id} not found.");

        return MapToDetailDto(incident);
    }

    // Lấy danh sách sự cố có bộ lọc tìm kiếm
    public async Task<List<IncidentListItemDto>> SearchAsync(
        IncidentQuery query, int requestingUserId, bool isAdmin, CancellationToken ct = default)
    {
        var q = _db.Incidents.AsNoTracking().AsQueryable();

        // Lọc theo người báo cáo nếu không phải admin (nếu muốn xem tất cả sự cố on-site của event thì bỏ qua)
        // Trong SRS, nhân viên hiện trường cùng xem sự cố trong event
        if (!isAdmin && query.IsEscalated != true)
        {
            // Cho phép Staff xem các sự cố
        }

        if (query.EventId.HasValue)
            q = q.Where(i => i.EventId == query.EventId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(i => i.Status.ToLower() == query.Status.ToLower());

        if (!string.IsNullOrWhiteSpace(query.Severity))
            q = q.Where(i => i.Severity.ToLower() == query.Severity.ToLower());

        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(i => i.Category.ToLower() == query.Category.ToLower());

        if (query.IsEscalated.HasValue)
            q = q.Where(i => i.IsEscalated == query.IsEscalated.Value);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            q = q.Where(i =>
                i.Title.ToLower().Contains(term) ||
                i.Location.ToLower().Contains(term) ||
                (i.EventName != null && i.EventName.ToLower().Contains(term)) ||
                i.ReporterName.ToLower().Contains(term) ||
                i.Description.ToLower().Contains(term));
        }

        var items = await q
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new IncidentListItemDto
            {
                Id = i.Id,
                EventId = i.EventId,
                EventName = i.EventName,
                ReporterId = i.ReporterId,
                ReporterName = i.ReporterName,
                Title = i.Title,
                Category = i.Category,
                Severity = i.Severity,
                Location = i.Location,
                Description = i.Description,
                Status = i.Status,
                IsEscalated = i.IsEscalated,
                EscalatedAt = i.EscalatedAt,
                EvidenceCount = i.Evidences.Count,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(ct);

        return items;
    }

    // UC_16.3: Cập nhật thông tin chi tiết sự cố và tiến độ
    public async Task<IncidentDetailDto> UpdateIncidentAsync(
        int id, UpdateIncidentRequest req, int updatedBy, string updaterName, bool isAdmin, CancellationToken ct = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (incident == null)
            throw new IncidentServiceException(404, $"Incident #{id} not found.");

        var now = DateTime.UtcNow;
        var prevStatus = incident.Status;

        if (!string.IsNullOrWhiteSpace(req.Title))
            incident.Title = req.Title.Trim();

        if (!string.IsNullOrWhiteSpace(req.Category))
            incident.Category = req.Category.Trim();

        if (!string.IsNullOrWhiteSpace(req.Severity))
            incident.Severity = req.Severity.Trim();

        if (!string.IsNullOrWhiteSpace(req.Location))
            incident.Location = req.Location.Trim();

        if (!string.IsNullOrWhiteSpace(req.Description))
            incident.Description = req.Description.Trim();

        if (!string.IsNullOrWhiteSpace(req.Status))
            incident.Status = req.Status.Trim();

        incident.UpdatedAt = now;

        var note = !string.IsNullOrWhiteSpace(req.UpdateNote)
            ? req.UpdateNote.Trim()
            : $"Incident details updated — status set to '{incident.Status}'";

        var updateRecord = new IncidentUpdate
        {
            IncidentId = incident.Id,
            UpdatedById = updatedBy,
            UpdatedByName = string.IsNullOrWhiteSpace(updaterName) ? "Staff" : updaterName,
            ActionType = "StatusUpdate",
            Note = note,
            PreviousStatus = prevStatus,
            NewStatus = incident.Status,
            CreatedAt = now
        };

        _db.IncidentUpdates.Add(updateRecord);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(id, updatedBy, true, ct);
    }

    // Cập nhật nhanh trạng thái
    public async Task<IncidentDetailDto> UpdateStatusAsync(
        int id, UpdateStatusRequest req, int updatedBy, string updaterName, bool isAdmin, CancellationToken ct = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (incident == null)
            throw new IncidentServiceException(404, $"Incident #{id} not found.");

        var prevStatus = incident.Status;
        incident.Status = req.Status.Trim();
        incident.UpdatedAt = DateTime.UtcNow;

        var updateRecord = new IncidentUpdate
        {
            IncidentId = incident.Id,
            UpdatedById = updatedBy,
            UpdatedByName = string.IsNullOrWhiteSpace(updaterName) ? "Staff" : updaterName,
            ActionType = "StatusChange",
            Note = !string.IsNullOrWhiteSpace(req.Note) ? req.Note.Trim() : $"Status changed: {prevStatus} ➔ {incident.Status}",
            PreviousStatus = prevStatus,
            NewStatus = incident.Status,
            CreatedAt = DateTime.UtcNow
        };

        _db.IncidentUpdates.Add(updateRecord);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(id, updatedBy, true, ct);
    }

    // UC_16.4: Chuyển cấp / Leo thang sự cố lên Admin
    public async Task<IncidentDetailDto> EscalateIncidentAsync(
        int id, EscalateIncidentRequest req, int staffId, string staffName, CancellationToken ct = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (incident == null)
            throw new IncidentServiceException(404, $"Incident #{id} not found.");

        if (incident.IsEscalated)
            throw new IncidentServiceException(409, "This incident has already been escalated to Admin.");

        var now = DateTime.UtcNow;
        var prevStatus = incident.Status;

        incident.IsEscalated = true;
        incident.Status = "Escalated";
        incident.EscalatedAt = now;
        incident.EscalationReason = req.Reason.Trim();
        if (req.AdminId.HasValue && req.AdminId.Value > 0)
            incident.EscalatedToAdminId = req.AdminId.Value;
        incident.UpdatedAt = now;

        var updateRecord = new IncidentUpdate
        {
            IncidentId = incident.Id,
            UpdatedById = staffId,
            UpdatedByName = string.IsNullOrWhiteSpace(staffName) ? "Staff" : staffName,
            ActionType = "Escalated",
            Note = $"Escalated to Admin: {req.Reason.Trim()}",
            PreviousStatus = prevStatus,
            NewStatus = "Escalated",
            CreatedAt = now
        };

        _db.IncidentUpdates.Add(updateRecord);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(id, staffId, true, ct);
    }

    // UC_16.5: Admin giải quyết sự cố
    public async Task<IncidentDetailDto> ResolveIncidentAsync(
        int id, ResolveIncidentRequest req, int adminId, string adminName, CancellationToken ct = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (incident == null)
            throw new IncidentServiceException(404, $"Incident #{id} not found.");

        var now = DateTime.UtcNow;
        var prevStatus = incident.Status;

        incident.Status = "Resolved";
        incident.ResolutionSummary = req.ResolutionSummary.Trim();
        incident.ActionTaken = req.ActionTaken.Trim();
        incident.ResolvedById = adminId;
        incident.ResolvedByName = string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName;
        incident.ResolvedAt = now;
        incident.UpdatedAt = now;

        var updateRecord = new IncidentUpdate
        {
            IncidentId = incident.Id,
            UpdatedById = adminId,
            UpdatedByName = incident.ResolvedByName,
            ActionType = "Resolved",
            Note = $"Incident resolved: {req.ResolutionSummary.Trim()}",
            PreviousStatus = prevStatus,
            NewStatus = "Resolved",
            CreatedAt = now
        };

        _db.IncidentUpdates.Add(updateRecord);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(id, adminId, true, ct);
    }

    // UC_16.5: Xem thông tin giải quyết sự cố
    public async Task<ResolutionDto> GetResolutionAsync(int id, CancellationToken ct = default)
    {
        var incident = await _db.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (incident == null)
            throw new IncidentServiceException(404, $"Incident #{id} not found.");

        if (incident.Status != "Resolved" && incident.Status != "Closed" && string.IsNullOrWhiteSpace(incident.ResolutionSummary))
            throw new IncidentServiceException(400, "This incident has not been resolved yet.");

        return new ResolutionDto
        {
            IncidentId = incident.Id,
            Title = incident.Title,
            ResolutionSummary = incident.ResolutionSummary ?? "No resolution summary provided.",
            ActionTaken = incident.ActionTaken ?? "Resolved on site.",
            ResolvedById = incident.ResolvedById,
            ResolvedByName = incident.ResolvedByName,
            ResolvedAt = incident.ResolvedAt
        };
    }

    // Lấy dòng thời gian cập nhật của sự cố
    public async Task<List<IncidentUpdateDto>> GetHistoryAsync(int id, CancellationToken ct = default)
    {
        var updates = await _db.IncidentUpdates
            .AsNoTracking()
            .Where(u => u.IncidentId == id)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new IncidentUpdateDto
            {
                Id = u.Id,
                IncidentId = u.IncidentId,
                UpdatedById = u.UpdatedById,
                UpdatedByName = u.UpdatedByName,
                ActionType = u.ActionType,
                Note = u.Note,
                PreviousStatus = u.PreviousStatus,
                NewStatus = u.NewStatus,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);

        return updates;
    }

    private static IncidentDetailDto MapToDetailDto(Incident i)
    {
        return new IncidentDetailDto
        {
            Id = i.Id,
            EventId = i.EventId,
            EventName = i.EventName,
            ReporterId = i.ReporterId,
            ReporterName = i.ReporterName,
            Title = i.Title,
            Category = i.Category,
            Severity = i.Severity,
            Location = i.Location,
            Description = i.Description,
            Status = i.Status,
            IsEscalated = i.IsEscalated,
            EscalatedToAdminId = i.EscalatedToAdminId,
            EscalatedAt = i.EscalatedAt,
            EscalationReason = i.EscalationReason,
            ResolutionSummary = i.ResolutionSummary,
            ActionTaken = i.ActionTaken,
            ResolvedById = i.ResolvedById,
            ResolvedByName = i.ResolvedByName,
            ResolvedAt = i.ResolvedAt,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt,
            Evidences = i.Evidences.Select(e => new IncidentEvidenceDto
            {
                Id = e.Id,
                IncidentId = e.IncidentId,
                FileUrl = e.FileUrl,
                PublicId = e.PublicId,
                FileName = e.FileName,
                FileType = e.FileType,
                FileSize = e.FileSize,
                MimeType = e.MimeType,
                Caption = e.Caption,
                UploadedById = e.UploadedById,
                UploadedByName = e.UploadedByName,
                UploadedAt = e.UploadedAt
            }).ToList(),
            Updates = i.Updates.OrderBy(u => u.CreatedAt).Select(u => new IncidentUpdateDto
            {
                Id = u.Id,
                IncidentId = u.IncidentId,
                UpdatedById = u.UpdatedById,
                UpdatedByName = u.UpdatedByName,
                ActionType = u.ActionType,
                Note = u.Note,
                PreviousStatus = u.PreviousStatus,
                NewStatus = u.NewStatus,
                CreatedAt = u.CreatedAt
            }).ToList()
        };
    }
}
