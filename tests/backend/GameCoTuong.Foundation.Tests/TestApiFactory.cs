using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GameCoTuong.Foundation.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // WebApplicationFactory otherwise defaults to Development, even in CI.
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GameDatabase"] = "",
                ["Jwt:SigningKey"] = "",
                ["BrowserClient:AllowedOrigins:0"] = "http://localhost:5173",
                ["BrowserClient:AllowedOrigins:1"] = "http://127.0.0.1:5173"
            }));
    }
}
