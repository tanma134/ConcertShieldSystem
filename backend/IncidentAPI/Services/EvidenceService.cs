using IncidentAPI.Data;
using IncidentAPI.DTOs;
using IncidentAPI.Models;
using IncidentAPI.Repositories;
using Microsoft.AspNetCore.Http;

namespace IncidentAPI.Services;

public class EvidenceService : IEvidenceService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly ICloudinaryService _cloudinary;
    private readonly ILogger<EvidenceService> _logger;

    private const string CloudinaryFolder = "concert-shield/incidents/evidence";
    private const int MaxEvidencesPerIncident = 30;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public EvidenceService(IIncidentRepository incidentRepository, ICloudinaryService cloudinary, ILogger<EvidenceService> logger)
    {
        _incidentRepository = incidentRepository;
        _cloudinary = cloudinary;
        _logger = logger;
    }

    public EvidenceService(IncidentDbContext db, ICloudinaryService cloudinary, ILogger<EvidenceService> logger)
        : this(new IncidentRepository(db), cloudinary, logger)
    {
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
        var incident = await _incidentRepository.GetIncidentByIdAsync(incidentId, ct)
            ?? throw new IncidentServiceException(404, $"Incident #{incidentId} not found.");

        var currentCount = await _incidentRepository.CountEvidenceByIncidentIdAsync(incidentId, ct);
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

        await _incidentRepository.AddEvidenceAsync(evidence, ct);

        // Ghi nhận nhật ký đính kèm bằng chứng
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
        await _incidentRepository.AddIncidentUpdateAsync(update, ct);

        await _incidentRepository.SaveChangesAsync(ct);

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
        var evidences = await _incidentRepository.GetEvidenceListAsync(incidentId, ct);
        return evidences.Select(e => new IncidentEvidenceDto
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
        }).ToList();
    }

    public async Task DeleteEvidenceAsync(int incidentId, int evidenceId, int requestingUserId, bool isAdmin, CancellationToken ct = default)
    {
        var evidence = await _incidentRepository.GetEvidenceByIdAsync(evidenceId, ct)
            ?? throw new IncidentServiceException(404, "Evidence not found.");

        if (evidence.IncidentId != incidentId)
            throw new IncidentServiceException(404, "Evidence not found.");

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

        await _incidentRepository.RemoveEvidenceAsync(evidence, ct);

        var update = new IncidentUpdate
        {
            IncidentId = incidentId,
            UpdatedById = requestingUserId,
            UpdatedByName = "User",
            ActionType = "EvidenceRemoved",
            Note = $"Evidence removed: {evidence.FileName}",
            CreatedAt = DateTime.UtcNow
        };
        await _incidentRepository.AddIncidentUpdateAsync(update, ct);

        await _incidentRepository.SaveChangesAsync(ct);
    }
}
