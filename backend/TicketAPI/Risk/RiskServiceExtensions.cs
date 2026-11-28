namespace TicketAPI.Risk;

public static class RiskServiceExtensions
{
    // Expected config: AdminApi:BaseUrl, AdminApi:InternalKey, and Risk:Enabled / Risk:AllowedOrigins.
    public static IServiceCollection AddRiskClient(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<SlidingCounter>();
        services.AddHttpClient<IRiskClient, RiskClient>(client =>
        {
            client.BaseAddress = new Uri(config["AdminApi:BaseUrl"] ?? "https://localhost:7076");
            var key = config["AdminApi:InternalKey"] ?? config["InternalApi:Key"];
            if (!string.IsNullOrEmpty(key)) client.DefaultRequestHeaders.Add("X-Internal-Api-Key", key);
        });
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddScoped<IChallengeVerifier, ChallengeVerifier>();
        services.AddScoped<IRiskGuard, RiskGuard>();
        return services;
    }
}
