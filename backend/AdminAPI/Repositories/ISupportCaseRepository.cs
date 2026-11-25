using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

/// Cung cấp truy cập dữ liệu Support Case cho Service.
public interface ISupportCaseRepository
{
    IQueryable<SupportCase> Cases { get; }
    IQueryable<SupportCaseNote> Notes { get; }
    void AddCase(SupportCase supportCase);
    void AddNote(SupportCaseNote note);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
