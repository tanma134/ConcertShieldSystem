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
        services.AddScoped<IFraudCaseService, FraudCaseService>();
        services.AddScoped<IRiskPolicyService, RiskPolicyService>();

        // AdminAPI -> TicketAPI internal calls (block/unblock/revoke a ticket).
        var ticketApiUrl = config["TicketApi:BaseUrl"] ?? "https://localhost:7268/";
        var ticketApiKey = config["TicketApi:InternalApiKey"] ?? "";
        services.AddHttpClient<ITicketRiskClient, TicketRiskClient>(c =>
        {
            c.BaseAddress = new Uri(ticketApiUrl);
            c.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrWhiteSpace(ticketApiKey))
                c.DefaultRequestHeaders.Add("X-Internal-Api-Key", ticketApiKey);
        });

        // AdminAPI -> AuthenticationAPI: verify that a fraud-case assignee is active Staff.
        var authApiUrl = config["AuthenticationApi:BaseUrl"] ?? "https://localhost:7058/";
        var authApiKey = config["AuthenticationApi:InternalApiKey"] ?? "";
        services.AddHttpClient<IStaffDirectoryClient, StaffDirectoryClient>(c =>
        {
            c.BaseAddress = new Uri(authApiUrl);
            c.Timeout = TimeSpan.FromSeconds(10);
            if (!string.IsNullOrWhiteSpace(authApiKey))
                c.DefaultRequestHeaders.Add("X-Internal-Api-Key", authApiKey);
        });

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