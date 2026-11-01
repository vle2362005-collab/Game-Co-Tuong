using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using GameCoTuong.Api.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameCoTuong.Foundation.Tests;

public sealed class LoggingAndSanitizationTests
{
    [Fact]
    public async Task Game_api_logs_trace_request_error_and_status_without_credentials()
    {
        var logs = new CaptureLogs();
        using var factory = new TestApiFactory { LoggerProvider = logs };
        using var client = factory.CreateClient();
        var requestId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        const string token = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzZWNyZXQifQ.secret";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/rooms/{roomId}?password=hidden");
        request.Headers.Add("X-Request-ID", requestId.ToString());
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var roomLog = Assert.Single(logs.Messages, message =>
            message.Contains("Game request rejected", StringComparison.Ordinal) &&
            message.Contains(roomId.ToString(), StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Trace-Id"));
        Assert.Contains(requestId.ToString(), roomLog);
        Assert.Contains(roomId.ToString(), roomLog);
        Assert.Contains(response.Headers.GetValues("X-Trace-Id").Single(), roomLog);
        Assert.Contains("UNAUTHORIZED", roomLog);
        Assert.Contains("401", roomLog);
        Assert.DoesNotContain(token, roomLog);
        Assert.DoesNotContain("hidden", roomLog);
        Assert.DoesNotContain(token, body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);

        using var matchRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/matches/{matchId}");
        matchRequest.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
        matchRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var matchResponse = await client.SendAsync(matchRequest);
        var matchLog = Assert.Single(logs.Messages, message =>
            message.Contains("Game request rejected", StringComparison.Ordinal) &&
            message.Contains(matchId.ToString(), StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, matchResponse.StatusCode);
        Assert.Contains(matchId.ToString(), matchLog);
    }

    [Fact]
    public async Task Unhandled_exception_is_logged_sanitized_and_returned_as_safe_error()
    {
        var logs = new CaptureLogs();
        const string token = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzZWNyZXQifQ.secret";
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureLogging(logging => logging.AddProvider(logs))
                .Configure(app =>
                {
                    app.UseMiddleware<SafeExceptionMiddleware>();
                    app.UseMiddleware<RequestLoggingMiddleware>();
                    app.Run(_ => Task.FromException(new InvalidOperationException($"password=hidden token={token}")));
                }))
            .StartAsync();
        using var client = host.GetTestClient();
        var requestId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Request-ID", requestId.ToString());

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var error = JsonDocument.Parse(body).RootElement.GetProperty("error");
        var exceptionLog = Assert.Single(logs.Messages,
            message => message.Contains("Unhandled request exception", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(requestId, JsonDocument.Parse(body).RootElement.GetProperty("requestId").GetGuid());
        Assert.Equal("INTERNAL_SERVER_ERROR", error.GetProperty("code").GetString());
        Assert.DoesNotContain(token, body);
        Assert.DoesNotContain("hidden", body);
        Assert.DoesNotContain(token, exceptionLog);
        Assert.DoesNotContain("hidden", exceptionLog);
        Assert.Contains("INTERNAL_SERVER_ERROR", exceptionLog);
        Assert.Contains("500", exceptionLog);
        Assert.True(response.Headers.Contains("X-Trace-Id"));
    }

    private sealed class CaptureLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
}
