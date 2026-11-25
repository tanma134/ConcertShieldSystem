using AdminAPI.Data;
using AdminAPI.Models;

namespace AdminAPI.Repositories;

/// Repository duy nhất chịu trách nhiệm đọc/ghi Support Case vào AdminDbContext.
public sealed class SupportCaseRepository : ISupportCaseRepository
{
    private readonly AdminDbContext _db;
    public SupportCaseRepository(AdminDbContext db) => _db = db;
    public IQueryable<SupportCase> Cases => _db.SupportCases;
    public IQueryable<SupportCaseNote> Notes => _db.SupportCaseNotes;
    public void AddCase(SupportCase supportCase) => _db.SupportCases.Add(supportCase);
    public void AddNote(SupportCaseNote note) => _db.SupportCaseNotes.Add(note);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
