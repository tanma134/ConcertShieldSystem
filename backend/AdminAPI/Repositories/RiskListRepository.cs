using AdminAPI.Data;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

public interface IRiskListRepository
{
    /// <summary>Tra whitelist/blacklist đang hiệu lực cho các giá trị (user id, device, IP, card hash).</summary>
    Task<(HashSet<(string Type, string Value)> White, HashSet<(string Type, string Value)> Black)> LookupAsync(
        IReadOnlyCollection<string> values, DateTime now, CancellationToken ct = default);

    /// <summary>Endpoint nội bộ: có blacklist cứng đang hiệu lực cho một trong các giá trị không.</summary>
    Task<bool> HasActiveBlacklistEntryAsync(IReadOnlyCollection<string> values, DateTime now,
        CancellationToken ct = default);
}

public sealed class RiskListRepository(AdminDbContext db) : IRiskListRepository
{
    public async Task<(HashSet<(string Type, string Value)> White, HashSet<(string Type, string Value)> Black)>
        LookupAsync(IReadOnlyCollection<string> values, DateTime now, CancellationToken ct = default)
    {
        var white = new HashSet<(string, string)>();
        var black = new HashSet<(string, string)>();
        if (values.Count == 0) return (white, black);

        var lookup = values.ToList();
        var rows = await db.RiskLists.AsNoTracking()
            .Where(l => (l.ExpiresAt == null || l.ExpiresAt > now) && lookup.Contains(l.ValueHash))
            .ToListAsync(ct);

        foreach (var l in rows)
            (l.ListType == "WHITELIST" ? white : black).Add((l.ValueType, l.ValueHash));
        return (white, black);
    }

    public Task<bool> HasActiveBlacklistEntryAsync(IReadOnlyCollection<string> values, DateTime now,
        CancellationToken ct = default)
    {
        if (values.Count == 0) return Task.FromResult(false);

        var lookup = values.ToList();
        return db.RiskLists.AsNoTracking().AnyAsync(l =>
            l.ListType == "BLACKLIST" && (l.ExpiresAt == null || l.ExpiresAt > now)
            && lookup.Contains(l.ValueHash)
            && (l.ValueType == "USER" || l.ValueType == "DEVICE" || l.ValueType == "IP"), ct);
    }
}
