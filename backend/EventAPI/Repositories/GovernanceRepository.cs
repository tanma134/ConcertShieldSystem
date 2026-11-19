using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventAPI.Repositories;

// Trien khai tat ca cac thao tac truy van du lieu va persistence cua module quan tri su kien
public class GovernanceRepository(EventDbContext db) : IGovernanceRepository
{
    // Lay su kien theo id co khoa row trong Postgres hoac doc binh thuong
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

    // Lay su kien khong theo doi de giam tai bo nho khi chi doc
    public async Task<Event?> GetEventAsNoTrackingAsync(int id, CancellationToken ct)
    {
        return await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id && !x.IsDeleted, ct);
    }

    // Kiem tra su kien co ton tai bat ky change request nao dang cho duyet
    public async Task<bool> HasPendingChangeRequestAsync(int eventId, CancellationToken ct)
    {
        return await db.Set<EventChangeRequest>().AnyAsync(x => x.EventId == eventId && x.Status == "Pending", ct);
    }

    // Them moi mot yeu cau thay doi lich trinh
    public async Task<EventChangeRequest> AddChangeRequestAsync(EventChangeRequest request, CancellationToken ct)
    {
        db.Set<EventChangeRequest>().Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    // Lay chi tiet yeu cau thay doi theo id va eventId
    public async Task<EventChangeRequest?> GetChangeRequestByIdAsync(int eventId, long requestId, CancellationToken ct)
    {
        return await db.Set<EventChangeRequest>().SingleOrDefaultAsync(x => x.Id == requestId && x.EventId == eventId, ct);
    }

    // Lay toan bo danh sach yeu cau thay doi cua mot su kien
    public async Task<List<EventChangeRequest>> GetChangeRequestsByEventIdAsync(int eventId, CancellationToken ct)
    {
        return await db.Set<EventChangeRequest>().AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    // Lay danh sach tat ca change requests tren toan he thong cho Admin
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
                        c.SubmittedAt,
                        c.ReviewedBy,
                        c.ReviewedAt,
                        c.ReviewNotes,
                        c.ProcessingStatus
                    };

        var items = await query.Take(200).ToListAsync(ct);
        return items.Cast<object>().ToList();
    }

    // Lay danh sach ticket types chua bi xoa cua su kien
    public async Task<List<TicketType>> GetActiveTicketTypesAsync(int eventId, CancellationToken ct)
    {
        return await db.TicketTypes.Where(x => x.EventId == eventId && !x.IsDeleted).ToListAsync(ct);
    }

    // Lay toan bo ho so tai lieu phap ly cua su kien
    public async Task<List<ComplianceDocument>> GetComplianceDocumentsAsync(int eventId, CancellationToken ct)
    {
        return await db.Set<ComplianceDocument>().AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(ct);
    }

    // Lay mot tai lieu phap ly cu the de tai ve
    public async Task<ComplianceDocument?> GetComplianceDocumentByIdAsync(int eventId, long documentId, CancellationToken ct)
    {
        return await db.Set<ComplianceDocument>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId && x.EventId == eventId, ct);
    }

    // Lay danh sach cac lan tham dinh ho so phap ly
    public async Task<List<ComplianceReview>> GetComplianceReviewsAsync(int eventId, CancellationToken ct)
    {
        return await db.Set<ComplianceReview>().AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(ct);
    }

    // Them mot tai lieu phap ly moi
    public async Task AddComplianceDocumentAsync(ComplianceDocument doc, CancellationToken ct)
    {
        db.Set<ComplianceDocument>().Add(doc);
        await Task.CompletedTask;
    }

    // Sao chep cac tai lieu phap ly sang phien ban moi
    public async Task AddComplianceDocumentsAsync(IEnumerable<ComplianceDocument> docs, CancellationToken ct)
    {
        db.Set<ComplianceDocument>().AddRange(docs);
        await Task.CompletedTask;
    }

    // Them ban ghi danh gia ho so phap ly
    public async Task AddComplianceReviewAsync(ComplianceReview review, CancellationToken ct)
    {
        db.Set<ComplianceReview>().Add(review);
        await Task.CompletedTask;
    }

    // Ghi ban ghi outbox xu ly tac vu nen
    public void AddOutbox(GovernanceOutbox outbox)
    {
        db.Set<GovernanceOutbox>().Add(outbox);
    }

    // Bat dau mot transaction an toan
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        return await db.Database.BeginTransactionAsync(ct);
    }

    // Luu cac thay doi du lieu
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        return await db.SaveChangesAsync(ct);
    }

    // Xoa sach change tracker khi rollback hoac huy thao tac
    public void ClearChangeTracker()
    {
        db.ChangeTracker.Clear();
    }
}
