using System.Collections.Concurrent;
using GameCoTuong.Application.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Hubs;

public sealed class LiveConnections
{
    private readonly ConcurrentDictionary<string, (Guid User, Guid? Room)> connections = new();
    public void Add(string connection, Guid user, Guid? room = null) => connections[connection] = (user, room);
    public (Guid User, Guid? Room)? Remove(string connection) => connections.TryRemove(connection, out var info) ? info : null;
    public bool Has(Guid user, Guid room) => connections.Values.Any(v => v.User == user && v.Room == room);
    public async Task EvictInactive(RoomView room, IHubContext<GameHub> hub)
    {
        foreach (var pair in connections.Where(p => p.Value.Room == room.Id && !room.Members.Any(m => m.UserId == p.Value.User)))
        {
            await hub.Groups.RemoveFromGroupAsync(pair.Key, GameHub.GetRoomGroupName(room.Id));
            connections[pair.Key] = (pair.Value.User, null);
        }
    }
}
