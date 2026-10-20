using IncidentAPI.DTOs;
using Microsoft.AspNetCore.Http;

namespace IncidentAPI.Services;

public interface IEvidenceService
{
    // UC_16.2: Upload tệp bằng chứng lên Cloudinary và lưu DB
    Task<IncidentEvidenceDto> UploadEvidenceAsync(
        int incidentId,
        IFormFile file,
        string? caption,
        int uploadedById,
        string uploadedByName,
        bool isAdmin,
        CancellationToken ct = default);

    // Lấy danh sách bằng chứng của một sự cố
    Task<List<IncidentEvidenceDto>> GetEvidencesAsync(int incidentId, CancellationToken ct = default);

    // Xóa một bằng chứng
    Task DeleteEvidenceAsync(int incidentId, int evidenceId, int requestingUserId, bool isAdmin, CancellationToken ct = default);
}
