using Microsoft.Extensions.DependencyInjection;

namespace AdminAPI.Repositories;

public static class RepositoryServiceCollectionExtensions
{
    /// <summary>Đăng ký toàn bộ tầng truy cập dữ liệu của AdminAPI.</summary>
    public static IServiceCollection AddAdminRepositories(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IFraudAlertRepository, FraudAlertRepository>();
        services.AddScoped<IRiskAppealRepository, RiskAppealRepository>();
        services.AddScoped<IRiskDecisionRepository, RiskDecisionRepository>();
        services.AddScoped<IRiskListRepository, RiskListRepository>();
        services.AddScoped<IRiskNotificationRepository, RiskNotificationRepository>();
        services.AddScoped<ISystemParameterRepository, SystemParameterRepository>();
        services.AddScoped<ISystemPolicyRepository, SystemPolicyRepository>();
        return services;
    }
}
