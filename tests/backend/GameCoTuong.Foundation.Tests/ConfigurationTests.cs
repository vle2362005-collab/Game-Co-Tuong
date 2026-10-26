using GameCoTuong.Api.Configuration;

namespace GameCoTuong.Foundation.Tests;

public sealed class ConfigurationTests
{
    [Theory]
    [InlineData("*")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com?token=secret")]
    [InlineData("https://user:password@example.com")]
    public void Cors_rejects_wildcards_paths_and_embedded_credentials(string origin)
    {
        Assert.False(new BrowserClientOptions { AllowedOrigins = [origin] }.HasValidOrigins());
    }
}
