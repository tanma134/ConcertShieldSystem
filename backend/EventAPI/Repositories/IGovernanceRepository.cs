using EventAPI.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventAPI.Repositories;

public interface IGovernanceRepository
{
    Task<Event?> GetEventByIdAsync(int id, bool locked, CancellationToken ct);

    Task<Event?> GetEventAsNoTrackingAsync(int id, CancellationToken ct);

    Task<bool> HasPendingChangeRequestAsync(int eventId, CancellationToken ct);

    Task<EventChangeRequest> AddChangeRequestAsync(EventChangeRequest request, CancellationToken ct);

    Task<EventChangeRequest?> GetChangeRequestByIdAsync(int eventId, long requestId, CancellationToken ct);

    Task<List<EventChangeRequest>> GetChangeRequestsByEventIdAsync(int eventId, CancellationToken ct);

    Task<List<object>> GetAllChangeRequestsAsync(string? status, CancellationToken ct);

    Task<List<TicketType>> GetActiveTicketTypesAsync(int eventId, CancellationToken ct);

    Task<List<ComplianceDocument>> GetComplianceDocumentsAsync(int eventId, CancellationToken ct);

    Task<ComplianceDocument?> GetComplianceDocumentByIdAsync(int eventId, long documentId, CancellationToken ct);

    Task<List<ComplianceReview>> GetComplianceReviewsAsync(int eventId, CancellationToken ct);

    Task AddComplianceDocumentAsync(ComplianceDocument doc, CancellationToken ct);

    Task AddComplianceDocumentsAsync(IEnumerable<ComplianceDocument> docs, CancellationToken ct);

    Task AddComplianceReviewAsync(ComplianceReview review, CancellationToken ct);

    void AddOutbox(GovernanceOutbox outbox);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);

    void ClearChangeTracker();
}
