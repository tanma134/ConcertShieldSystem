using System.Net.Http;
using System.Text;
using System.Text.Json;
using IncidentAPI.Data;
using IncidentAPI.DTOs;
using IncidentAPI.Models;
using IncidentAPI.Repositories;
using Microsoft.Extensions.Configuration;

namespace IncidentAPI.Services;

// Triển khai toàn bộ nghiệp vụ Incident Management cho 5 Use Case ()
public class IncidentService : IIncidentService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly ILogger<IncidentService> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IConfiguration? _configuration;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public IncidentService(
        IIncidentRepository incidentRepository,
        ILogger<IncidentService> logger,
        IHttpClientFactory? httpClientFactory = null,
        IConfiguration? configuration = null)
    {
        _incidentRepository = incidentRepository;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public IncidentService(IIncidentRepository incidentRepository, ILogger<IncidentService> logger)
        : this(incidentRepository, logger, null, null)
    {
    }

    public IncidentService(IncidentDbContext db, ILogger<IncidentService> logger)
        : this(new IncidentRepository(db), logger, null, null)
    {
    }

    // Staff báo cáo sự cố mới tại hiện trường
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

        await _incidentRepository.AddIncidentAsync(incident, ct);

        // Ghi nhật ký ban đầu
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

        await _incidentRepository.AddIncidentUpdateAsync(initialUpdate, ct);

        return await GetDetailAsync(incident.Id, reporterId, true, ct);
    }

    // Lấy chi tiết sự cố kèm bằng chứng và nhật ký
    public async Task<IncidentDetailDto> GetDetailAsync(
        int id, int requestingUserId, bool isAdmin, CancellationToken ct = default)
    {
        var incident = await _incidentRepository.GetIncidentDetailAsync(id, ct);

        if (incident == null)
            throw new IncidentServiceException(404, $"Incident #{id} not found.");

        return MapToDetailDto(incident);
    }

    // Lấy danh sách sự cố có bộ lọc tìm kiếm
    public async Task<List<IncidentListItemDto>> SearchAsync(
        IncidentQuery query, int requestingUserId, bool isAdmin, CancellationToken ct = default)
    {
        return await _incidentRepository.SearchIncidentsAsync(query, requestingUserId, isAdmin, ct);
    }

    // Cập nhật thông tin chi tiết sự cố và tiến độ
    public async Task<IncidentDetailDto> UpdateIncidentAsync(
        int id, UpdateIncidentRequest req, int updatedBy, string updaterName, bool isAdmin, CancellationToken ct = default)
    {
        var incident = await _incidentRepository.GetIncidentByIdAsync(id, ct);
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

        await _incidentRepository.AddIncidentUpdateAsync(updateRecord, ct);
        await _incidentRepository.SaveChangesAsync(ct);

        return await GetDetailAsync(id, updatedBy, true, ct);
    }

    // Cập nhật nhanh trạng thái
    public async Task<IncidentDetailDto> UpdateStatusAsync(
        int id, UpdateStatusRequest req, int updatedBy, string updaterName, bool isAdmin, CancellationToken ct = default)
    {
        var incident = await _incidentRepository.GetIncidentByIdAsync(id, ct);
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

        await _incidentRepository.AddIncidentUpdateAsync(updateRecord, ct);
        await _incidentRepository.SaveChangesAsync(ct);

        return await GetDetailAsync(id, updatedBy, true, ct);
    }

    // Chuyển cấp / Leo thang sự cố lên Admin
    public async Task<IncidentDetailDto> EscalateIncidentAsync(
        int id, EscalateIncidentRequest req, int staffId, string staffName, CancellationToken ct = default)
    {
        var incident = await _incidentRepository.GetIncidentByIdAsync(id, ct);
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

        await _incidentRepository.AddIncidentUpdateAsync(updateRecord, ct);
        await _incidentRepository.SaveChangesAsync(ct);

        _logger.LogInformation("Incident #{IncidentId} escalated to Admin {AdminId} by staff {StaffId}", id, incident.EscalatedToAdminId ?? 1, staffId);

        // Gui thong bao den Admin qua NotificationAPI (ho tro retry va idempotency key)
        var targetAdminId = incident.EscalatedToAdminId ?? 1;
        _ = Task.Run(async () =>
        {
            try
            {
                var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
                var baseUrl = _configuration?["NotificationApi:BaseUrl"] ?? "http://localhost:7197";
                var notifPayload = new
                {
                    userId = targetAdminId,
                    title = $"[URGENT] Incident #{incident.Id} Escalated: {incident.Title}",
                    message = $"Staff {staffName} escalated incident #{incident.Id} ({incident.Category}/{incident.Severity}). Reason: {req.Reason.Trim()}",
                    category = "incident_escalated",
                    targetUrl = $"/admin/incidents/{incident.Id}"
                };
                using var content = new StringContent(JsonSerializer.Serialize(notifPayload), Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.Remove("Idempotency-Key");
                client.DefaultRequestHeaders.Add("Idempotency-Key", $"incident-escalate-{incident.Id}");
                var response = await client.PostAsync($"{baseUrl.TrimEnd('/')}/api/notifications", content);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Escalation notification dispatched for incident {IncidentId} to admin {AdminId}", incident.Id, targetAdminId);
                }
                else
                {
                    _logger.LogWarning("Failed to dispatch escalation notification for incident {IncidentId}, status: {StatusCode}", incident.Id, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not notify admin {AdminId} for escalated incident {IncidentId}", targetAdminId, incident.Id);
            }
        });

        return await GetDetailAsync(id, staffId, true, ct);
    }

    // Admin giải quyết sự cố
    public async Task<IncidentDetailDto> ResolveIncidentAsync(
        int id, ResolveIncidentRequest req, int adminId, string adminName, CancellationToken ct = default)
    {
        var incident = await _incidentRepository.GetIncidentByIdAsync(id, ct);
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

        await _incidentRepository.AddIncidentUpdateAsync(updateRecord, ct);
        await _incidentRepository.SaveChangesAsync(ct);

        return await GetDetailAsync(id, adminId, true, ct);
    }

    // Xem thông tin giải quyết sự cố
    public async Task<ResolutionDto> GetResolutionAsync(int id, CancellationToken ct = default)
    {
        var incident = await _incidentRepository.GetIncidentDetailAsync(id, ct);

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
        var updates = await _incidentRepository.GetIncidentHistoryAsync(id, ct);

        return updates.Select(u => new IncidentUpdateDto
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
        }).ToList();
    }

    private static IncidentDetailDto MapToDetailDto(Incident incident)
    {
        return new IncidentDetailDto
        {
            Id = incident.Id,
            EventId = incident.EventId,
            EventName = incident.EventName,
            ReporterId = incident.ReporterId,
            ReporterName = incident.ReporterName,
            Title = incident.Title,
            Category = incident.Category,
            Severity = incident.Severity,
            Location = incident.Location,
            Description = incident.Description,
            Status = incident.Status,
            IsEscalated = incident.IsEscalated,
            EscalationReason = incident.EscalationReason,
            EscalatedAt = incident.EscalatedAt,
            EscalatedToAdminId = incident.EscalatedToAdminId,
            ResolutionSummary = incident.ResolutionSummary,
            ActionTaken = incident.ActionTaken,
            ResolvedById = incident.ResolvedById,
            ResolvedByName = incident.ResolvedByName,
            ResolvedAt = incident.ResolvedAt,
            CreatedAt = incident.CreatedAt,
            UpdatedAt = incident.UpdatedAt,
            Evidences = incident.Evidences.Select(e => new IncidentEvidenceDto
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
            Updates = incident.Updates.OrderBy(u => u.CreatedAt).Select(u => new IncidentUpdateDto
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
