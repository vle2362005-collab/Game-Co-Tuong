using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Api.Hubs;

public sealed class SessionHubFilter(GameDbContext db) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = context.Context.User;
        var jti = user?.FindFirst("jti")?.Value;
        var exp = user?.FindFirst("exp")?.Value;
        if (user?.Identity?.IsAuthenticated != true || !long.TryParse(exp, out var seconds) || seconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
            await db.RevokedTokens.AnyAsync(t => t.Jti == jti))
        {
            context.Context.Abort(); throw new HubException("UNAUTHORIZED: Phiên đã hết hạn.");
        }
        return await next(context);
    }
}
