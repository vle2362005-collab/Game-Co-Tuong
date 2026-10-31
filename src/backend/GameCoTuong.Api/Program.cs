using GameCoTuong.Api.Configuration;
using GameCoTuong.Api.Endpoints;
using GameCoTuong.Api.Hubs;
using GameCoTuong.Api.Middleware;
using GameCoTuong.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}
// Keep environment variables and CLI arguments above local files/user secrets.
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);

builder.Services.AddOptions<BrowserClientOptions>()
    .BindConfiguration(BrowserClientOptions.SectionName)
    .Validate(options => options.HasValidOrigins(),
        "BrowserClient:AllowedOrigins must contain exact http/https origins without paths or wildcards.")
    .ValidateOnStart();

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("BrowserClient:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<LiveConnections>();
builder.Services.AddSingleton<VideoCalls>();
builder.Services.AddSignalR(options => options.AddFilter<SessionHubFilter>());
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
if (app.Configuration["Database:Provider"] == "Sqlite")
{
    using var scope = app.Services.CreateScope();
    GameCoTuong.Infrastructure.Game.GameService.WriteGate.Wait();
    try { scope.ServiceProvider.GetRequiredService<GameDbContext>().Database.EnsureCreated(); }
    finally { GameCoTuong.Infrastructure.Game.GameService.WriteGate.Release(); }
}
_ = app.Services.GetRequiredService<IOptions<BrowserClientOptions>>().Value;
app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseMiddleware<JwtAuthenticationMiddleware>();

// Process liveness only: this does not claim that SQL Server or game services are ready.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        service = "game-co-tuong-api"
    }, context.RequestAborted)
});

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapRoomEndpoints();
app.MapMatchEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();

public partial class Program { }
