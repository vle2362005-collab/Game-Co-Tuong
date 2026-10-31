using System.Text.Json;
using GameCoTuong.Api.Endpoints;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Common.Interfaces;

namespace GameCoTuong.Api.Middleware;

public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public JwtAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, GameCoTuong.Infrastructure.Persistence.GameDbContext dbContext)
    {
        string? token = null;

        // 1. Check Authorization header
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader.Substring("Bearer ".Length).Trim();
        }

        // 2. Check query string access_token (for SignalR and WebSockets)
        if (string.IsNullOrWhiteSpace(token) && context.Request.Path.StartsWithSegments("/hubs/game") && context.Request.Query.TryGetValue("access_token", out var queryToken))
        {
            token = queryToken.ToString().Trim();
        }

        // 3. Validate token if present
        if (!string.IsNullOrWhiteSpace(token))
        {
            var principal = tokenService.ValidateToken(token);
            if (principal is not null)
            {
                var jti = principal.FindFirst("jti")?.Value;
                var isRevoked = !string.IsNullOrEmpty(jti) && await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(dbContext.RevokedTokens, r => r.Jti == jti);

                if (isRevoked)
                {
                    context.Items["TokenRevoked"] = true;
                }
                else
                {
                    context.User = principal;
                }
            }
            else
            {
                context.Items["TokenInvalid"] = true;
            }
        }

        // 4. Protect SignalR hub endpoint: require valid authentication
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/hubs/game", StringComparison.OrdinalIgnoreCase))
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                var reqId = AuthEndpoints.ExtractRequestId(context);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var error = new ApiErrorEnvelope(
                    reqId,
                    DateTimeOffset.UtcNow,
                    new ApiErrorDetail(
                        "UNAUTHORIZED",
                        "Kết nối SignalR yêu cầu JWT hợp lệ qua Authorization header hoặc query parameter access_token.",
                        false,
                        null,
                        null,
                        new Dictionary<string, string[]>()
                    )
                );

                await context.Response.WriteAsync(JsonSerializer.Serialize(error));
                return;
            }
        }

        await _next(context);
    }
}
