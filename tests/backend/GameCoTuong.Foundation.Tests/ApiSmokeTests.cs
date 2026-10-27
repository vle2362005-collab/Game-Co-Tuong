using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameCoTuong.Foundation.Tests;

public sealed class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public ApiSmokeTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Health_runs_without_database_or_jwt_secret()
    {
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(health);
        Assert.Equal("Unhealthy", health["status"]); // GCT-23 temporary CI failure probe
        Assert.Equal("game-co-tuong-api", health["service"]);
        Assert.Equal(2, health.Count);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1:5173")]
    public async Task Cors_allows_only_configured_frontend(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData("https://untrusted.example")]
    [InlineData("http://localhost:5174")]
    public async Task Cors_does_not_allow_an_unconfigured_origin(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Unimplemented_business_endpoint_is_not_a_fake_success()
    {
        using var response = await client.GetAsync("/api/v1/rooms");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
