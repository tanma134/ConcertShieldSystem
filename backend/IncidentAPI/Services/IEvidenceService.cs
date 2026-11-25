using IncidentAPI.DTOs;
using Microsoft.AspNetCore.Http;

namespace IncidentAPI.Services;

public interface IEvidenceService
{
    Task<IncidentEvidenceDto> UploadEvidenceAsync(
        int incidentId,
        IFormFile file,
        string? caption,
        int uploadedById,
        string uploadedByName,
        bool isAdmin,
        CancellationToken ct = default);

    Task<List<IncidentEvidenceDto>> GetEvidencesAsync(int incidentId, CancellationToken ct = default);

    Task DeleteEvidenceAsync(int incidentId, int evidenceId, int requestingUserId, bool isAdmin, CancellationToken ct = default);
}
