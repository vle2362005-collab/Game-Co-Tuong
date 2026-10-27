using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameCoTuong.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GameDatabase");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<GameDbContext>(options =>
                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(GameDbContext).Assembly.FullName)));
        }

        return services;
    }
}
