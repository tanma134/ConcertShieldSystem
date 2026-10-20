namespace TicketAPI.Risk;

public static class RiskServiceExtensions
{
    /// <summary>
    /// Cấu hình cần có trong appsettings:
    ///   "AdminApi": { "BaseUrl": "https://localhost:7076", "InternalKey": "..." }   // InternalKey = AdminAPI "InternalApi:Key"
    ///   "Risk": { "Enabled": true, "AllowedOrigins": [ "http://localhost:5173" ] }
    /// </summary>
    public static IServiceCollection AddRiskClient(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<SlidingCounter>();
        services.AddHttpClient<IRiskClient, RiskClient>(client =>
        {
            client.BaseAddress = new Uri(config["AdminApi:BaseUrl"] ?? "https://localhost:7076");
            var key = config["AdminApi:InternalKey"] ?? config["InternalApi:Key"];
            if (!string.IsNullOrEmpty(key)) client.DefaultRequestHeaders.Add("X-Internal-Api-Key", key);
        });
        services.AddScoped<IRiskGuard, RiskGuard>();
        return services;
    }
}
