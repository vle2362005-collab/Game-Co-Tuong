using GameCoTuong.Application.Game;
using GameCoTuong.Infrastructure.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Hubs;

public sealed class MatchEventNotifier(IHubContext<GameHub> hub, GameService game) : IDrawOfferExpirationNotifier
{
    public async Task NotifyExpired(Guid matchId, Guid roomId, CancellationToken cancellationToken)
    {
        var state = await game.MatchStateSnapshot(matchId);
        await hub.Clients.Group(GameHub.GetRoomGroupName(roomId))
            .SendAsync("MatchChanged", state, cancellationToken);
    }
}
