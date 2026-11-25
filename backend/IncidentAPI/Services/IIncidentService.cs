using IncidentAPI.DTOs;

namespace IncidentAPI.Services;

public interface IIncidentService
{
    Task<IncidentDetailDto> CreateIncidentAsync(CreateIncidentRequest req, int reporterId, string reporterName, CancellationToken ct = default);

    Task<IncidentDetailDto> GetDetailAsync(int id, int requestingUserId, bool isAdmin, CancellationToken ct = default);

    Task<List<IncidentListItemDto>> SearchAsync(IncidentQuery query, int requestingUserId, bool isAdmin, CancellationToken ct = default);

    Task<IncidentDetailDto> UpdateIncidentAsync(int id, UpdateIncidentRequest req, int updatedBy, string updaterName, bool isAdmin, CancellationToken ct = default);

    Task<IncidentDetailDto> UpdateStatusAsync(int id, UpdateStatusRequest req, int updatedBy, string updaterName, bool isAdmin, CancellationToken ct = default);

    Task<IncidentDetailDto> EscalateIncidentAsync(int id, EscalateIncidentRequest req, int staffId, string staffName, CancellationToken ct = default);

    Task<IncidentDetailDto> ResolveIncidentAsync(int id, ResolveIncidentRequest req, int adminId, string adminName, CancellationToken ct = default);

    Task<ResolutionDto> GetResolutionAsync(int id, CancellationToken ct = default);

    Task<List<IncidentUpdateDto>> GetHistoryAsync(int id, CancellationToken ct = default);
}
