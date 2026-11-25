using IncidentAPI.DTOs;
using IncidentAPI.Models;

namespace IncidentAPI.Repositories;

public interface IIncidentRepository
{
    Task<Incident> AddIncidentAsync(Incident incident, CancellationToken ct = default);
    Task AddIncidentUpdateAsync(IncidentUpdate update, CancellationToken ct = default);
    Task<Incident?> GetIncidentDetailAsync(int id, CancellationToken ct = default);
    Task<Incident?> GetIncidentByIdAsync(int id, CancellationToken ct = default);
    Task<List<IncidentListItemDto>> SearchIncidentsAsync(IncidentQuery query, int requestingUserId, bool isAdmin, CancellationToken ct = default);
    Task<List<IncidentUpdate>> GetIncidentHistoryAsync(int incidentId, CancellationToken ct = default);
    Task<int> CountEvidenceByIncidentIdAsync(int incidentId, CancellationToken ct = default);
    Task AddEvidenceAsync(IncidentEvidence evidence, CancellationToken ct = default);
    Task<List<IncidentEvidence>> GetEvidenceListAsync(int incidentId, CancellationToken ct = default);
    Task<IncidentEvidence?> GetEvidenceByIdAsync(int evidenceId, CancellationToken ct = default);
    Task RemoveEvidenceAsync(IncidentEvidence evidence, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
