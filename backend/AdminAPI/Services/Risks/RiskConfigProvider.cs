using AdminAPI.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace AdminAPI.Services.Risks;


public sealed class RiskConfigProvider : IRiskConfigProvider
{
    private const string CacheKey = "risk:config";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly string[] Categories = { "risk_config", "risk_rule" };

    private readonly ISystemParameterRepository _parameters;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RiskConfigProvider> _logger;

    public RiskConfigProvider(ISystemParameterRepository parameters, IMemoryCache cache, ILogger<RiskConfigProvider> logger)
    {
        _parameters = parameters;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RiskConfig> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out RiskConfig? cached) && cached != null)
            return cached;

        var rows = await _parameters.GetKeyValuesAsync(Categories, ct);

        var cfg = RiskConfig.Parse(rows.Select(r => new KeyValuePair<string, string>(r.Key, r.Value)));

        foreach (var w in cfg.ParseWarnings)
            _logger.LogWarning("Risk config: {Warning}", w);

        _cache.Set(CacheKey, cfg, Ttl);
        return cfg;
    }

    public void Invalidate() => _cache.Remove(CacheKey);
}
