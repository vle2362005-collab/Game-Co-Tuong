using System.Security.Claims;
using GameCoTuong.Application.Auth.Services;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Hubs;

public class GameHub : Hub
{
    public const string LobbyGroupName = "lobby";
    public static string GetRoomGroupName(Guid roomId) => $"room-{roomId}";

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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    // ========================================================================
    // 1. Lobby Group Management
    // ========================================================================

    public async Task JoinLobby()
    {
        var userId = GetRequiredCallerUserId();
        if (Groups is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroupName);
        }

        if (Clients?.Caller is not null)
        {
            await Clients.Caller.SendAsync("LobbyJoined", new
            {
                userId,
                joinedAt = DateTimeOffset.UtcNow
            });
        }
    }

    public async Task LeaveLobby()
    {
        var userId = GetRequiredCallerUserId();
        if (Groups is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroupName);
        }

        if (Clients?.Caller is not null)
        {
            await Clients.Caller.SendAsync("LobbyLeft", new
            {
                userId,
                leftAt = DateTimeOffset.UtcNow
            });
        }
    }

    // ========================================================================
    // 2. Room Group Management with Authorization
    // ========================================================================

    public async Task JoinRoom(Guid roomId)
    {
        var userId = GetRequiredCallerUserId();

        // Kiểm tra quyền thành viên phòng trước khi gia nhập nhóm
        var authResult = await _roomAuthService.AuthorizeRoomMemberAsync(roomId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage ?? "Bạn không phải là thành viên của phòng này."}");
        }

        var groupName = GetRoomGroupName(roomId);
        if (Groups is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        if (Clients is not null)
        {
            // Chỉ phát sự kiện trong nhóm phòng tương ứng (Room isolation)
            await Clients.Group(groupName).SendAsync("PlayerJoinedRoom", new
            {
                roomId,
                userId,
                joinedAt = DateTimeOffset.UtcNow
            });

            await Clients.Caller.SendAsync("RoomJoined", new
            {
                roomId,
                groupName
            });
        }
    }

    public async Task LeaveRoom(Guid roomId)
    {
        var userId = GetRequiredCallerUserId();
        var groupName = GetRoomGroupName(roomId);

        if (Groups is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        if (Clients is not null)
        {
            await Clients.Group(groupName).SendAsync("PlayerLeftRoom", new
            {
                roomId,
                userId,
                leftAt = DateTimeOffset.UtcNow
            });

            await Clients.Caller.SendAsync("RoomLeft", new { roomId });
        }
    }

    public async Task SendRoomMessage(Guid roomId, string content)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeRoomMemberAsync(roomId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage ?? "Bạn không phải là thành viên của phòng này."}");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new HubException("VALIDATION_ERROR: Nội dung tin nhắn không được để trống.");
        }

        var groupName = GetRoomGroupName(roomId);
        if (Clients is not null)
        {
            // Chỉ gửi tới phòng này, tuyệt đối không truyền sang phòng khác hay sảnh
            await Clients.Group(groupName).SendAsync("RoomMessageReceived", new
            {
                roomId,
                senderId = userId,
                content = content.Trim(),
                sentAt = DateTimeOffset.UtcNow
            });
        }
    }

    // ========================================================================
    // 3. Room Host Actions
    // ========================================================================

    public async Task StartRoom(Guid roomId)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeRoomHostAsync(roomId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        var groupName = GetRoomGroupName(roomId);
        if (Clients is not null)
        {
            await Clients.Group(groupName).SendAsync("RoomStarted", new
            {
                roomId,
                startedBy = userId,
                started = true,
                startedAt = DateTimeOffset.UtcNow
            });

            await Clients.Caller.SendAsync("RoomStarted", new { roomId, started = true });
            await Clients.Group(LobbyGroupName).SendAsync("RoomStateChanged", new { roomId, status = "playing" });
        }
    }

    public async Task CloseRoom(Guid roomId)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeRoomHostAsync(roomId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        var groupName = GetRoomGroupName(roomId);
        if (Clients is not null)
        {
            await Clients.Group(groupName).SendAsync("RoomClosed", new
            {
                roomId,
                closedBy = userId,
                closed = true,
                closedAt = DateTimeOffset.UtcNow
            });

            await Clients.Caller.SendAsync("RoomClosed", new { roomId, closed = true });
            await Clients.Group(LobbyGroupName).SendAsync("RoomStateChanged", new { roomId, status = "closed" });
        }
    }

    // ========================================================================
    // 4. Gameplay & Video Signaling
    // ========================================================================

    public async Task SubmitMove(Guid matchId, string moveNotation)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeMatchTurnAsync(matchId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        if (Clients?.Caller is not null)
        {
            await Clients.Caller.SendAsync("MoveAccepted", new { matchId, moveNotation });
        }
    }

    public async Task SubmitMoveToRoom(Guid roomId, Guid matchId, string moveNotation)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeMatchTurnAsync(matchId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        var groupName = GetRoomGroupName(roomId);
        if (Clients is not null)
        {
            await Clients.Group(groupName).SendAsync("MoveAccepted", new
            {
                roomId,
                matchId,
                playedBy = userId,
                moveNotation,
                playedAt = DateTimeOffset.UtcNow
            });
        }
    }

    public async Task SendVideoSignal(Guid roomId, string signalData)
    {
        var userId = GetRequiredCallerUserId();
        var authResult = await _roomAuthService.AuthorizeVideoCallAsync(roomId, userId);
        if (!authResult.IsAuthorized)
        {
            throw new HubException($"FORBIDDEN: {authResult.ErrorMessage}");
        }

        if (string.IsNullOrWhiteSpace(signalData))
        {
            throw new HubException("VALIDATION_ERROR: Dữ liệu tín hiệu video không được để trống.");
        }

        var groupName = GetRoomGroupName(roomId);
        if (Clients is not null)
        {
            await Clients.Group(groupName).SendAsync("VideoSignalReceived", new
            {
                roomId,
                senderId = userId,
                signalData,
                sentAt = DateTimeOffset.UtcNow
            });

            await Clients.Caller.SendAsync("VideoSignalSent", new { roomId, sent = true });
        }
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

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
