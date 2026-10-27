using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Realtime;

[Authorize]
public sealed class MatchHub(
    MatchReconnectService reconnectService,
    MatchConnectionRegistry connectionRegistry) : Hub
{
    public async Task<MatchReconnectState> ReconnectMatch(Guid matchId)
    {
        var userId = GetAuthenticatedUserId();
        using var transition = await connectionRegistry.AcquirePlayerTransitionAsync(
            matchId, userId, Context.ConnectionAborted);

        if (!connectionRegistry.CanTrack(Context.ConnectionId, matchId, userId))
        {
            throw new HubException("Một kết nối chỉ được tham gia một trận đấu.");
        }

        MatchReconnectResult result;
        try
        {
            result = await reconnectService.ReconnectAsync(
                matchId, userId, Context.ConnectionAborted);
        }
        catch (MatchAccessDeniedException)
        {
            throw new HubException("Không tìm thấy trận đấu hoặc bạn không có quyền truy cập.");
        }

        if (!result.Accepted)
        {
            if (result.State.Status is "ended" or "cancelled")
            {
                await Clients.Group(GroupName(matchId)).SendAsync(
                    "MatchEnded", result.State, Context.ConnectionAborted);
                return result.State;
            }

            throw new HubException("Không thể tiếp tục trận đấu; trận đã kết thúc hoặc hết thời gian nối lại.");
        }

        connectionRegistry.Track(Context.ConnectionId, matchId, userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(matchId), Context.ConnectionAborted);

        if (result.WasReconnected)
        {
            await Clients.Group(GroupName(matchId)).SendAsync(
                "PlayerReconnected", result.State, Context.ConnectionAborted);
        }

        return result.State;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (connectionRegistry.UntrackLastConnection(Context.ConnectionId, out var matchId, out var userId))
            {
                using var transition = await connectionRegistry.AcquirePlayerTransitionAsync(
                    matchId, userId, CancellationToken.None);
                if (!connectionRegistry.HasConnections(matchId, userId))
                {
                    var state = await reconnectService.MarkDisconnectedAsync(
                        matchId, userId, CancellationToken.None);
                    if (state is not null)
                    {
                        await Clients.Group(GroupName(matchId))
                            .SendAsync("PlayerDisconnected", state);
                        await Clients.Group(GroupName(matchId)).SendAsync("MatchPaused", state);
                    }
                }
            }
        }
        finally
        {
            await base.OnDisconnectedAsync(exception);
        }
    }

    internal static string GroupName(Guid matchId) => $"match:{matchId:N}";

    private Guid GetAuthenticatedUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(claim, out var userId))
        {
            return userId;
        }

        throw new HubException("Không xác định được người chơi đã đăng nhập.");
    }
}
