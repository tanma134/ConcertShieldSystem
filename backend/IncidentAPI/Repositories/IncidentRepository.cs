using IncidentAPI.Data;
using IncidentAPI.DTOs;
using IncidentAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace IncidentAPI.Repositories;

public class IncidentRepository : IIncidentRepository
{
    private readonly IncidentDbContext _db;

    public IncidentRepository(IncidentDbContext db)
    {
        _db = db;
    }

    public async Task<Incident> AddIncidentAsync(Incident incident, CancellationToken ct = default)
    {
        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync(ct);
        return incident;
    }

    public async Task AddIncidentUpdateAsync(IncidentUpdate update, CancellationToken ct = default)
    {
        _db.IncidentUpdates.Add(update);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Incident?> GetIncidentDetailAsync(int id, CancellationToken ct = default)
    {
        return await _db.Incidents
            .Include(i => i.Evidences)
            .Include(i => i.Updates)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<Incident?> GetIncidentByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<List<IncidentListItemDto>> SearchIncidentsAsync(
        IncidentQuery query, int requestingUserId, bool isAdmin, CancellationToken ct = default)
    {
        var q = _db.Incidents.AsNoTracking().AsQueryable();

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

        return await q
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
    }

    public async Task<List<IncidentUpdate>> GetIncidentHistoryAsync(int incidentId, CancellationToken ct = default)
    {
        return await _db.IncidentUpdates
            .AsNoTracking()
            .Where(u => u.IncidentId == incidentId)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> CountEvidenceByIncidentIdAsync(int incidentId, CancellationToken ct = default)
    {
        return await _db.IncidentEvidences.CountAsync(e => e.IncidentId == incidentId, ct);
    }

    public async Task AddEvidenceAsync(IncidentEvidence evidence, CancellationToken ct = default)
    {
        await _db.IncidentEvidences.AddAsync(evidence, ct);
    }

    public async Task<List<IncidentEvidence>> GetEvidenceListAsync(int incidentId, CancellationToken ct = default)
    {
        return await _db.IncidentEvidences
            .AsNoTracking()
            .Where(e => e.IncidentId == incidentId)
            .OrderByDescending(e => e.UploadedAt)
            .ToListAsync(ct);
    }

    public async Task<IncidentEvidence?> GetEvidenceByIdAsync(int evidenceId, CancellationToken ct = default)
    {
        return await _db.IncidentEvidences.FirstOrDefaultAsync(e => e.Id == evidenceId, ct);
    }

    public Task RemoveEvidenceAsync(IncidentEvidence evidence, CancellationToken ct = default)
    {
        _db.IncidentEvidences.Remove(evidence);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }
}
