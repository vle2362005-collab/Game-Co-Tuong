using System.Security.Claims;
using GameCoTuong.Api.Reconnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Hubs;

[Authorize]
public sealed class MatchHub(
    MatchReconnectService reconnectService,
    MatchConnectionRegistry connectionRegistry,
    ILogger<MatchHub> logger) : Hub
{
    public static string GetMatchGroup(Guid matchId) => $"match:{matchId:N}";

    public async Task<MatchReconnectState> Reconnect(Guid matchId)
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? Context.User?.FindFirstValue("userId");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new HubException("Token không chứa UserId hợp lệ.");
        }

        using var gate = await connectionRegistry.AcquireAsync(
            matchId, userId, Context.ConnectionAborted);
        var state = await reconnectService.ReconnectAsync(matchId, userId, Context.ConnectionAborted);
        connectionRegistry.Add(matchId, userId, Context.ConnectionId);
        Context.Items["matchId"] = matchId;
        Context.Items["userId"] = userId;
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetMatchGroup(matchId), Context.ConnectionAborted);
        }
        catch
        {
            if (connectionRegistry.Remove(matchId, userId, Context.ConnectionId))
            {
                await reconnectService.HandleDisconnectAsync(matchId, userId, CancellationToken.None);
            }

            throw;
        }

        await Clients.Caller.SendAsync("MatchStateRestored", state, Context.ConnectionAborted);
        await Clients.OthersInGroup(GetMatchGroup(matchId))
            .SendAsync("MatchPlayerReconnected", new { state.MatchId, UserId = userId, state.Side },
                Context.ConnectionAborted);
        return state;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("matchId", out var matchValue)
            && matchValue is Guid matchId
            && Context.Items.TryGetValue("userId", out var userValue)
            && userValue is Guid userId)
        {
            try
            {
                using var gate = await connectionRegistry.AcquireAsync(
                    matchId, userId, CancellationToken.None);
                if (connectionRegistry.Remove(matchId, userId, Context.ConnectionId))
                {
                    await reconnectService.HandleDisconnectAsync(matchId, userId, CancellationToken.None);
                }
            }
            catch (Exception error)
            {
                logger.LogError(error,
                    "Failed to persist disconnect for match {MatchId} and user {UserId}.",
                    matchId, userId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
