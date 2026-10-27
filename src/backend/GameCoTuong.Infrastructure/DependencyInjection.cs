using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Infrastructure.Auth;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameCoTuong.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GameDatabase")
            ?? "Server=.\\SQLEXPRESS;Database=GameCoTuong;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

        services.AddDbContext<GameDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(GameDbContext).Assembly.FullName)));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IRegistrationService, RegistrationService>();

        return services;
    }
}
