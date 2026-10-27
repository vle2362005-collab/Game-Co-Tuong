using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameCoTuong.Api.Realtime;

public static class MatchReconnectRegistration
{
    public static IServiceCollection AddMatchReconnect(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<MatchConnectionRegistry>();
        services.AddScoped<MatchReconnectService>();
        services.AddHostedService<ReconnectDeadlineWorker>();
        return services;
    }
}
