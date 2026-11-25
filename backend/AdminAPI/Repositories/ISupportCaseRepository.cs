using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

public interface ISupportCaseRepository
{
    IQueryable<SupportCase> Cases { get; }
    IQueryable<SupportCaseNote> Notes { get; }
    void AddCase(SupportCase supportCase);
    void AddNote(SupportCaseNote note);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
