using Microsoft.Extensions.DependencyInjection;

namespace AdminAPI.Services.Risks;

public static class RiskServiceCollectionExtensions
{
    public static IServiceCollection AddRiskEngine(this IServiceCollection services, IConfiguration config)
    {
        services.AddMemoryCache();
        services.AddScoped<IRiskConfigProvider, RiskConfigProvider>();
        services.AddSingleton<RiskScoreCalculator>();
        services.AddScoped<IFraudAlertService, FraudAlertService>();

        var timeout = TimeSpan.FromSeconds(30);
        if (string.Equals(config["Ai:Provider"], "Anthropic", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<IAiChatClient, AnthropicChatClient>(c => c.Timeout = timeout);
        else
            services.AddHttpClient<IAiChatClient, OpenAiCompatibleChatClient>(c => c.Timeout = timeout);

        services.AddScoped<IAiAnalysisService, AiAnalysisService>();
        services.AddSingleton<AiSummaryQueue>();
        services.AddSingleton<IAiSummaryQueue>(sp => sp.GetRequiredService<AiSummaryQueue>());
        services.AddHostedService<AiSummaryWorker>();
        return services;
    }
}