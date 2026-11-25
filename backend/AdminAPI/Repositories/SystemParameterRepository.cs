using AdminAPI.Data;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

public sealed class SystemParameterRepository(AdminDbContext db) : ISystemParameterRepository
{
    public async Task<List<(string Key, string Value)>> GetKeyValuesAsync(string[] categories, CancellationToken ct = default)
    {
        var rows = await db.SystemParameters
            .AsNoTracking()
            .Where(p => categories.Contains(p.Category))
            .Select(p => new { p.ParamKey, p.ParamValue })
            .ToListAsync(ct);

        return rows.Select(r => (r.ParamKey, r.ParamValue)).ToList();
    }

    public Task<List<SystemParameter>> GetOrderedAsync(string[] categories, string? category, CancellationToken ct = default)
    {
        var q = db.SystemParameters.AsNoTracking().Where(p => categories.Contains(p.Category));
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(p => p.Category == category);

        return q.OrderBy(p => p.Category).ThenBy(p => p.ParamKey).ToListAsync(ct);
    }

    public Task<List<SystemParameter>> GetForUpdateAsync(string[] categories) =>
        db.SystemParameters.Where(p => categories.Contains(p.Category)).ToListAsync();

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
