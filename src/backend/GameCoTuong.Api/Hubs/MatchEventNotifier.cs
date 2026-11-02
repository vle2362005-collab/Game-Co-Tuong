using GameCoTuong.Application.Game;
using GameCoTuong.Infrastructure.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Hubs;

public sealed class MatchEventNotifier(IHubContext<GameHub> hub, GameService game, LiveConnections live, VideoCalls calls) : IDrawOfferExpirationNotifier, IRoomCleanupNotifier
{
    public async Task NotifyClosed(RoomView room, CancellationToken cancellationToken)
    {
        calls.End(room.Id, room.HostUserId);
        await hub.Clients.Group(GameHub.GetRoomGroupName(room.Id)).SendAsync("CallEnded", cancellationToken);
        await hub.Clients.Group(GameHub.GetRoomGroupName(room.Id))
            .SendAsync("RoomStateChanged", room, cancellationToken);
        await hub.Clients.Group(GameHub.LobbyGroupName)
            .SendAsync("LobbyChanged", cancellationToken);
        await live.EvictInactive(room, hub);
    }

    public async Task NotifyExpired(Guid matchId, Guid roomId, CancellationToken cancellationToken)
    {
        var state = await game.MatchStateSnapshot(matchId);
        await hub.Clients.Group(GameHub.GetRoomGroupName(roomId))
            .SendAsync("MatchChanged", state, cancellationToken);
    }
}
