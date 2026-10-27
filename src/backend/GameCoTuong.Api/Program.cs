using GameCoTuong.Api.Configuration;
using GameCoTuong.Api.Hubs;
using GameCoTuong.Api.Reconnect;
using GameCoTuong.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;

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
var hasGameDatabase = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("GameDatabase"));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MatchConnectionRegistry>();
builder.Services.AddSingleton<ReconnectTimeoutRegistry>();
if (hasGameDatabase)
{
    builder.Services.AddScoped<MatchReconnectService>();
}

var signingKey = builder.Configuration["Jwt:SigningKey"];
if (!string.IsNullOrWhiteSpace(signingKey) && Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = string.IsNullOrWhiteSpace(signingKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]),
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.NameIdentifier
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/matches"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
_ = app.Services.GetRequiredService<IOptions<BrowserClientOptions>>().Value;
app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

// Process liveness only: this does not claim that SQL Server or game services are ready.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        service = "game-co-tuong-api"
    }, context.RequestAborted)
});
app.MapHub<MatchHub>("/hubs/matches");

app.Run();

public partial class Program { }
