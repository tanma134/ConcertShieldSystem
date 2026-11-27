using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventAPI.Repositories;

public class GovernanceRepository(EventDbContext db) : IGovernanceRepository
{
    public async Task<Event?> GetEventByIdAsync(int id, bool locked, CancellationToken ct)
    {
        if (locked && db.Database.IsNpgsql())
        {
            var ev = await db.Events
                .FromSqlInterpolated($"SELECT *, xmin FROM events WHERE event_id = {id} AND NOT is_deleted FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (ev != null)
            {
                await db.Entry(ev).ReloadAsync(ct);
            }
            return ev;
        }

        return await db.Events.SingleOrDefaultAsync(x => x.EventId == id && !x.IsDeleted, ct);
    }

    public async Task<Event?> GetEventAsNoTrackingAsync(int id, CancellationToken ct)
    {
        return await db.Events.AsNoTracking().Include(x => x.TicketTypes)
            .SingleOrDefaultAsync(x => x.EventId == id && !x.IsDeleted, ct);
    }

    public async Task<bool> HasPendingChangeRequestAsync(int eventId, CancellationToken ct)
    {
        return await db.Set<EventChangeRequest>().AnyAsync(x => x.EventId == eventId && x.Status == "Pending", ct);
    }

    public async Task<EventChangeRequest> AddChangeRequestAsync(EventChangeRequest request, CancellationToken ct)
    {
        db.Set<EventChangeRequest>().Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<EventChangeRequest?> GetChangeRequestByIdAsync(int eventId, long requestId, CancellationToken ct)
    {
        return await db.Set<EventChangeRequest>().SingleOrDefaultAsync(x => x.Id == requestId && x.EventId == eventId, ct);
    }

    public async Task<List<EventChangeRequest>> GetChangeRequestsByEventIdAsync(int eventId, CancellationToken ct)
    {
        return await db.Set<EventChangeRequest>().AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<List<object>> GetAllChangeRequestsAsync(string? status, CancellationToken ct)
    {
        var query = from c in db.Set<EventChangeRequest>().AsNoTracking()
                    join e in db.Events.AsNoTracking() on c.EventId equals e.EventId
                    where string.IsNullOrEmpty(status) || c.Status == status
                    orderby c.Id descending
                    select new
                    {
                        c.Id,
                        c.EventId,
                        EventTitle = e.Title,
                        e.OrganizerId,
                        e.Timezone,
                        EventStatus = e.Status,
                        c.Type,
                        c.Status,
                        c.Reason,
                        c.OldStartsAt,
                        c.OldEndsAt,
                        c.NewStartsAt,
                        c.NewEndsAt,
                        c.EvidenceUrl,
                        c.SubmittedAt,
                        c.ReviewedBy,
                        c.ReviewedAt,
                        c.ReviewNotes,
                        c.ProcessingStatus
                    };

        var items = await query.Take(200).ToListAsync(ct);
        return items.Cast<object>().ToList();
    }

    public async Task<List<TicketType>> GetActiveTicketTypesAsync(int eventId, CancellationToken ct)
    {
        return await db.TicketTypes.Where(x => x.EventId == eventId && !x.IsDeleted).ToListAsync(ct);
    }

    public async Task<List<ComplianceDocument>> GetComplianceDocumentsAsync(int eventId, CancellationToken ct)
    {
        return await db.Set<ComplianceDocument>().AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(ct);
    }

    public async Task<ComplianceDocument?> GetComplianceDocumentByIdAsync(int eventId, long documentId, CancellationToken ct)
    {
        return await db.Set<ComplianceDocument>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId && x.EventId == eventId, ct);
    }

    public async Task<List<ComplianceReview>> GetComplianceReviewsAsync(int eventId, CancellationToken ct)
    {
        return await db.Set<ComplianceReview>().AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(ct);
    }

    public async Task AddComplianceDocumentAsync(ComplianceDocument doc, CancellationToken ct)
    {
        db.Set<ComplianceDocument>().Add(doc);
        await Task.CompletedTask;
    }

    public async Task AddComplianceDocumentsAsync(IEnumerable<ComplianceDocument> docs, CancellationToken ct)
    {
        db.Set<ComplianceDocument>().AddRange(docs);
        await Task.CompletedTask;
    }

    public async Task AddComplianceReviewAsync(ComplianceReview review, CancellationToken ct)
    {
        db.Set<ComplianceReview>().Add(review);
        await Task.CompletedTask;
    }

    public void AddOutbox(GovernanceOutbox outbox)
    {
        db.Set<GovernanceOutbox>().Add(outbox);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        return await db.Database.BeginTransactionAsync(ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        return await db.SaveChangesAsync(ct);
    }

    public void ClearChangeTracker()
    {
        db.ChangeTracker.Clear();
    }
}
