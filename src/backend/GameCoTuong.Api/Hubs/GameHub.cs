using System.Security.Claims;
using System.Text.Json;
using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Application.Game;
using GameCoTuong.Infrastructure.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Hubs;

public class GameHub(IRoomAuthorizationService auth, GameService game, LiveConnections live, VideoCalls calls) : Hub
{
    public const string LobbyGroupName = "lobby";
    public static string GetRoomGroupName(Guid id) => $"room-{id}";
    public Guid? GetCallerUserId() => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub"), out var id) ? id : null;
    private Guid UserId => GetCallerUserId() ?? throw new HubException("UNAUTHORIZED: Cần đăng nhập.");

    public override Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true || GetCallerUserId() is null) { Context.Abort(); return Task.CompletedTask; }
        live.Add(Context.ConnectionId, UserId);
        return base.OnConnectedAsync();
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var info = live.Remove(Context.ConnectionId);
        if (info?.Room is Guid roomId && !live.Has(info.Value.User, roomId))
        {
            try
            {
                var room = await game.Presence(roomId, info.Value.User, false);
                calls.End(roomId, info.Value.User);
                await Clients.Group(GetRoomGroupName(roomId)).SendAsync("CallEnded");
                await Clients.Group(GetRoomGroupName(roomId)).SendAsync("RoomStateChanged", room);
            }
            catch (GameFault) { }
        }
        await base.OnDisconnectedAsync(exception);
    }
    public async Task JoinLobby()
    {
        _ = UserId;
        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroupName);
        await Clients.Caller.SendAsync("LobbyJoined", new { userId = UserId });
    }
    public Task LeaveLobby() => Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroupName);
    public async Task JoinRoom(Guid roomId)
    {
        var permission = await auth.AuthorizeRoomMemberAsync(roomId, UserId);
        if (!permission.IsAuthorized) throw new HubException($"FORBIDDEN: {permission.ErrorMessage}");
        live.Add(Context.ConnectionId, UserId, roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GetRoomGroupName(roomId));
        var room = await game.Presence(roomId, UserId, true);
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("PlayerJoinedRoom", new { roomId, userId = UserId });
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("RoomStateChanged", room);
    }
    public async Task LeaveRoom(Guid roomId)
    {
        live.Add(Context.ConnectionId, UserId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetRoomGroupName(roomId));
        if (!live.Has(UserId, roomId))
        {
            calls.End(roomId, UserId);
            await Clients.Group(GetRoomGroupName(roomId)).SendAsync("CallEnded");
            try { await Clients.Group(GetRoomGroupName(roomId)).SendAsync("RoomStateChanged", await game.Presence(roomId, UserId, false)); }
            catch (GameFault) { }
        }
    }
    public async Task SendRoomMessage(Guid roomId, string content, Guid? requestId = null)
    {
        await RequireMember(roomId);
        var message = await HubRun(() => game.Chat(roomId, UserId, new ChatInput(requestId ?? Guid.NewGuid(), content)));
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("RoomMessageReceived", message);
    }
    public async Task StartRoom(Guid roomId)
    {
        await RequireMember(roomId);
        var room = await HubRun(() => game.Start(roomId, UserId, new CommandInput(Guid.NewGuid())));
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("RoomStateChanged", room);
        await Clients.Group(LobbyGroupName).SendAsync("LobbyChanged");
    }
    public async Task CloseRoom(Guid roomId)
    {
        await RequireMember(roomId);
        var room = await HubRun(() => game.Leave(roomId, UserId, true));
        calls.End(roomId, UserId);
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("CallEnded");
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("RoomStateChanged", room);
        await Clients.Group(LobbyGroupName).SendAsync("LobbyChanged");
    }
    public async Task Signal(Guid roomId, SignalInput signal)
    {
        await RequireMember(roomId);
        var room = await game.Snapshot(roomId, UserId);
        if (!room.Members.Any(m => m.UserId == signal.RecipientId) || !live.Has(signal.RecipientId, roomId))
            throw new HubException("FORBIDDEN: Người nhận không trực tuyến trong phòng.");
        try { calls.Validate(roomId, UserId, signal); }
        catch (GameFault ex) { throw new HubException($"{ex.Code}: {ex.Message}"); }
        await Clients.User(signal.RecipientId.ToString()).SendAsync("VideoSignalReceived", new
        { roomId, senderId = UserId, signal.CallId, signal.RecipientId, signal.Type, signal.Data });
    }
    // Old clients must provide a complete envelope; arbitrary strings can no longer start a call.
    public async Task SendVideoSignal(Guid roomId, string signalData)
    {
        await RequireMember(roomId);
        try
        {
            var signal = JsonSerializer.Deserialize<SignalInput>(signalData, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (signal is null) throw new JsonException();
            await Signal(roomId, signal);
        }
        catch (JsonException) { throw new HubException("INVALID_SIGNAL: Thiếu mã cuộc gọi hoặc người nhận."); }
    }
    private async Task RequireMember(Guid roomId)
    {
        var result = await auth.AuthorizeRoomMemberAsync(roomId, UserId);
        if (!result.IsAuthorized) throw new HubException($"FORBIDDEN: {result.ErrorMessage}");
    }
    private static async Task<T> HubRun<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (GameFault ex) { throw new HubException($"{ex.Code}: {ex.Message}"); }
    }
}
