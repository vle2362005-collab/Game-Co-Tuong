using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Api.Hubs;

public sealed class SessionHubFilter(GameDbContext db, ILogger<SessionHubFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = context.Context.User;
        var jti = user?.FindFirst("jti")?.Value;
        var exp = user?.FindFirst("exp")?.Value;
        var expired = !long.TryParse(exp, out var seconds) || seconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var revoked = !expired && await db.RevokedTokens.AnyAsync(t => t.Jti == jti);
        if (user?.Identity?.IsAuthenticated != true || expired || revoked)
        {
            logger.LogWarning(
                "SignalR invocation rejected for expired or revoked session. {TraceId} {UserId} {ConnectionId} {ErrorCode} {StatusCode}",
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.Context.GetHttpContext()?.TraceIdentifier,
                context.Context.UserIdentifier, context.Context.ConnectionId, "SESSION_EXPIRED", StatusCodes.Status401Unauthorized);
            context.Context.Abort(); throw new HubException("UNAUTHORIZED: Phiên đã hết hạn.");
        }
        return await next(context);
    }
}
