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
        services.Configure<JwtOptions>(options =>
        {
            var section = configuration.GetSection(JwtOptions.SectionName);
            var issuer = section["Issuer"];
            if (!string.IsNullOrEmpty(issuer)) options.Issuer = issuer;
            var audience = section["Audience"];
            if (!string.IsNullOrEmpty(audience)) options.Audience = audience;
            var signingKey = section["SigningKey"];
            if (!string.IsNullOrEmpty(signingKey)) options.SigningKey = signingKey;
            if (int.TryParse(section["ExpirationMinutes"], out var exp) && exp > 0)
            {
                options.ExpirationMinutes = exp;
            }
        });
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRoomAuthorizationService, RoomAuthorizationService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<ILoginService, LoginService>();

        return services;
    }
}
