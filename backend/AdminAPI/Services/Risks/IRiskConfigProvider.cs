
namespace AdminAPI.Services.Risks
{
    public interface IRiskConfigProvider
    {
        Task<RiskConfig> GetAsync(CancellationToken ct = default);

        void Invalidate();
    }
}
