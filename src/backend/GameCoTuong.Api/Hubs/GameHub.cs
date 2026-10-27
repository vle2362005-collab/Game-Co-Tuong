using System.Security.Claims;
using GameCoTuong.Application.Auth.Services;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Hubs;

public class GameHub : Hub
{
    private readonly IRoomAuthorizationService _roomAuthService;

    public GameHub(IRoomAuthorizationService roomAuthService)
    {
        _roomAuthService = roomAuthService;
    }

    public override Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true || GetCallerUserId() is null)
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        return base.OnConnectedAsync();
    }

    public async Task StartRoom(Guid roomId)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeRoomHostAsync(roomId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        await Clients.Caller.SendAsync("RoomStarted", new { roomId, started = true });
    }

    public async Task CloseRoom(Guid roomId)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeRoomHostAsync(roomId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        await Clients.Caller.SendAsync("RoomClosed", new { roomId, closed = true });
    }

    public async Task SubmitMove(Guid matchId, string moveNotation)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeMatchTurnAsync(matchId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        await Clients.Caller.SendAsync("MoveAccepted", new { matchId, moveNotation });
    }

    public async Task SendVideoSignal(Guid roomId, string signalData)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeVideoCallAsync(roomId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        await Clients.Caller.SendAsync("VideoSignalSent", new { roomId, sent = true });
    }

    public Guid? GetCallerUserId()
    {
        var sub = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? Context.User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(sub, out var parsedId))
        {
            return parsedId;
        }

        return null;
    }

    private Guid GetRequiredCallerUserId()
    {
        var userId = GetCallerUserId();
        if (userId is null)
        {
            throw new HubException("UNAUTHORIZED: Phiên làm việc không chứa danh tính người dùng hợp lệ.");
        }
        return userId.Value;
    }
}
