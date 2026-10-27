using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GameCoTuong.Foundation.Tests;

public sealed class TestIsolationTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    [Fact]
    public void Api_test_host_uses_testing_environment_without_database_or_jwt_credentials()
    {
        Assert.Equal("Testing", factory.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("", configuration.GetConnectionString("GameDatabase"));
        Assert.Equal("", configuration["Jwt:SigningKey"]);
    }
}
