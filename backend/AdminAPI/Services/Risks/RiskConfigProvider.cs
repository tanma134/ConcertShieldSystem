using AdminAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AdminAPI.Services.Risks;


public sealed class RiskConfigProvider : IRiskConfigProvider
{
    private const string CacheKey = "risk:config";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly AdminDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RiskConfigProvider> _logger;

    public RiskConfigProvider(AdminDbContext db, IMemoryCache cache, ILogger<RiskConfigProvider> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RiskConfig> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out RiskConfig? cached) && cached != null)
            return cached;

        var rows = await _db.SystemParameters
            .AsNoTracking()
            .Where(p => p.Category == "risk_config" || p.Category == "risk_rule")
            .Select(p => new { p.ParamKey, p.ParamValue })
            .ToListAsync(ct);

        var cfg = RiskConfig.Parse(rows.Select(r => new KeyValuePair<string, string>(r.ParamKey, r.ParamValue)));

        foreach (var w in cfg.ParseWarnings)
            _logger.LogWarning("Risk config: {Warning}", w);

        _cache.Set(CacheKey, cfg, Ttl);
        return cfg;
    }

    public void Invalidate() => _cache.Remove(CacheKey);
}