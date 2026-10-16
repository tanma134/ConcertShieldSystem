using IncidentAPI.Data;
using IncidentAPI.DTOs;
using IncidentAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace IncidentAPI.Services;

public class EvidenceService : IEvidenceService
{
    private readonly IncidentDbContext _db;
    private readonly ICloudinaryService _cloudinary;
    private readonly ILogger<EvidenceService> _logger;

    private const string CloudinaryFolder = "concert-shield/incidents/evidence";
    private const int MaxEvidencesPerIncident = 30;

    public EvidenceService(IncidentDbContext db, ICloudinaryService cloudinary, ILogger<EvidenceService> logger)
    {
        _db = db;
        _cloudinary = cloudinary;
        _logger = logger;
    }

    public async Task<IncidentEvidenceDto> UploadEvidenceAsync(
        int incidentId,
        IFormFile file,
        string? caption,
        int uploadedById,
        string uploadedByName,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var incident = await _db.Incidents
            .FirstOrDefaultAsync(i => i.Id == incidentId, ct)
            ?? throw new IncidentServiceException(404, $"Incident #{incidentId} not found.");

        var currentCount = await _db.IncidentEvidences
            .CountAsync(e => e.IncidentId == incidentId, ct);
        if (currentCount >= MaxEvidencesPerIncident)
            throw new IncidentServiceException(400, $"This incident has reached the maximum of {MaxEvidencesPerIncident} evidence files.");

        // Upload lên Cloudinary
        var uploadResult = await _cloudinary.UploadFileAsync(file, $"{CloudinaryFolder}/{incidentId}", ct);

        var fileType = (uploadResult.MimeType ?? "").StartsWith("video/") ? "video"
            : (uploadResult.MimeType ?? "").StartsWith("audio/") ? "audio"
            : (uploadResult.MimeType ?? "").Contains("pdf") ? "document"
            : "image";

        var evidence = new IncidentEvidence
        {
            IncidentId = incidentId,
            FileUrl = uploadResult.FileUrl,
            PublicId = uploadResult.PublicId,
            FileName = file.FileName,
            FileType = fileType,
            FileSize = uploadResult.FileSize,
            MimeType = uploadResult.MimeType ?? "application/octet-stream",
            Caption = caption?.Trim(),
            UploadedById = uploadedById,
            UploadedByName = string.IsNullOrWhiteSpace(uploadedByName) ? "Staff" : uploadedByName,
            UploadedAt = DateTime.UtcNow
        };

        _db.IncidentEvidences.Add(evidence);

        // Ghi nhận nhật ký đính kèm bằng chứng (UC_16.3)
        var update = new IncidentUpdate
        {
            IncidentId = incidentId,
            UpdatedById = uploadedById,
            UpdatedByName = evidence.UploadedByName,
            ActionType = "EvidenceAdded",
            Note = $"Evidence uploaded: {evidence.FileName} ({evidence.FileType})",
            PreviousStatus = incident.Status,
            NewStatus = incident.Status,
            CreatedAt = DateTime.UtcNow
        };
        _db.IncidentUpdates.Add(update);

        await _db.SaveChangesAsync(ct);

        return new IncidentEvidenceDto
        {
            Id = evidence.Id,
            IncidentId = evidence.IncidentId,
            FileUrl = evidence.FileUrl,
            PublicId = evidence.PublicId,
            FileName = evidence.FileName,
            FileType = evidence.FileType,
            FileSize = evidence.FileSize,
            MimeType = evidence.MimeType,
            Caption = evidence.Caption,
            UploadedById = evidence.UploadedById,
            UploadedByName = evidence.UploadedByName,
            UploadedAt = evidence.UploadedAt
        };
    }

    public async Task<List<IncidentEvidenceDto>> GetEvidencesAsync(int incidentId, CancellationToken ct = default)
    {
        return await _db.IncidentEvidences
            .AsNoTracking()
            .Where(e => e.IncidentId == incidentId)
            .OrderByDescending(e => e.UploadedAt)
            .Select(e => new IncidentEvidenceDto
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
            })
            .ToListAsync(ct);
    }

    public async Task DeleteEvidenceAsync(int incidentId, int evidenceId, int requestingUserId, bool isAdmin, CancellationToken ct = default)
    {
        var evidence = await _db.IncidentEvidences
            .FirstOrDefaultAsync(e => e.Id == evidenceId && e.IncidentId == incidentId, ct)
            ?? throw new IncidentServiceException(404, "Evidence not found.");

        if (!isAdmin && evidence.UploadedById != requestingUserId)
            throw new IncidentServiceException(403, "You do not have permission to delete this evidence.");

        // Xóa trên Cloudinary
        try
        {
            await _cloudinary.DeleteFileAsync(evidence.PublicId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file from Cloudinary {PublicId}", evidence.PublicId);
        }

        _db.IncidentEvidences.Remove(evidence);

        var update = new IncidentUpdate
        {
            IncidentId = incidentId,
            UpdatedById = requestingUserId,
            UpdatedByName = "User",
            ActionType = "EvidenceRemoved",
            Note = $"Evidence removed: {evidence.FileName}",
            CreatedAt = DateTime.UtcNow
        };
        _db.IncidentUpdates.Add(update);

        await _db.SaveChangesAsync(ct);
    }
}
