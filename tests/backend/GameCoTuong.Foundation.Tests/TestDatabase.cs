using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GameCoTuong.Foundation.Tests;

internal static class TestDatabase
{
    internal static readonly string ConnectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"gct-tests-{Guid.NewGuid():N}.db")};Default Timeout=30";
    internal static WebApplicationFactory<Program> Configure(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:GameDatabase"] = ConnectionString
            }));
        });
    internal static GameDbContext Create() => new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite(ConnectionString).Options);
}
