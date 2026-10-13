using AdminAPI.Services.Risks;
using Microsoft.Extensions.DependencyInjection;

namespace AdminAPI.Services.Risks;

public static class AppealServiceCollectionExtensions
{
    public static IServiceCollection AddAppealEngine(this IServiceCollection services)
    {
        services.AddScoped<IAppealService, AppealService>();
        return services;
    }
}