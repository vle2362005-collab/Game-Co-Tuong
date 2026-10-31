using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Infrastructure.Auth;

public class RoomAuthorizationService : IRoomAuthorizationService
{
    private readonly GameDbContext _dbContext;

    public RoomAuthorizationService(GameDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthorizationResult> AuthorizeRoomHostAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default)
    {
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

        if (room is null)
        {
            return AuthorizationResult.Failed("ROOM_NOT_FOUND", "Không tìm thấy phòng tương ứng.");
        }

        if (string.Equals(room.Status, "closed", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationResult.Failed("ROOM_CLOSED", "Phòng đã đóng.");
        }

        if (room.HostUserId != userId)
        {
            return AuthorizationResult.Failed("NOT_ROOM_HOST", "Chỉ chủ phòng mới có quyền thực hiện thao tác này.");
        }

        var isMember = await _dbContext.RoomMemberships
            .AsNoTracking()
            .AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.LeftAt == null, cancellationToken);

        if (!isMember)
        {
            return AuthorizationResult.Failed("NOT_ROOM_MEMBER", "Người dùng không còn là thành viên hoạt động trong phòng.");
        }

        return AuthorizationResult.Succeeded();
    }

    public async Task<AuthorizationResult> AuthorizeRoomMemberAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default)
    {
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

        if (room is null)
        {
            return AuthorizationResult.Failed("ROOM_NOT_FOUND", "Không tìm thấy phòng tương ứng.");
        }

        if (string.Equals(room.Status, "closed", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationResult.Failed("ROOM_CLOSED", "Phòng đã đóng.");
        }

        var isMember = await _dbContext.RoomMemberships
            .AsNoTracking()
            .AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.LeftAt == null, cancellationToken);

        if (!isMember)
        {
            return AuthorizationResult.Failed("NOT_ROOM_MEMBER", "Người dùng không phải thành viên hoạt động của phòng này.");
        }

        return AuthorizationResult.Succeeded();
    }

    public async Task<AuthorizationResult> AuthorizeMatchPlayerAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);

        if (match is null)
        {
            return AuthorizationResult.Failed("MATCH_NOT_FOUND", "Không tìm thấy ván đấu tương ứng.");
        }

        var isPlayer = await _dbContext.MatchPlayers
            .AsNoTracking()
            .AnyAsync(mp => mp.MatchId == matchId && mp.UserId == userId, cancellationToken);

        if (!isPlayer)
        {
            return AuthorizationResult.Failed("NOT_MATCH_PLAYER", "Người dùng không tham gia ván đấu này.");
        }

        return AuthorizationResult.Succeeded();
    }

    public async Task<AuthorizationResult> AuthorizeMatchTurnAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default)
    {
        var match = await _dbContext.Matches
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);

        if (match is null)
        {
            return AuthorizationResult.Failed("MATCH_NOT_FOUND", "Không tìm thấy ván đấu tương ứng.");
        }

        if (!string.Equals(match.Status, "playing", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationResult.Failed("MATCH_NOT_PLAYING", "Ván đấu không ở trạng thái sẵn sàng đi quân.");
        }

        var player = await _dbContext.MatchPlayers
            .AsNoTracking()
            .FirstOrDefaultAsync(mp => mp.MatchId == matchId && mp.UserId == userId, cancellationToken);

        if (player is null)
        {
            return AuthorizationResult.Failed("NOT_MATCH_PLAYER", "Người dùng không tham gia ván đấu này.");
        }

        if (!string.Equals(player.Side, match.SideToMove, StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationResult.Failed("NOT_YOUR_TURN", "Chưa đến lượt đi của bạn.");
        }

        return AuthorizationResult.Succeeded();
    }

    public async Task<AuthorizationResult> AuthorizeVideoCallAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default)
    {
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

        if (room is null)
        {
            return AuthorizationResult.Failed("ROOM_NOT_FOUND", "Không tìm thấy phòng tương ứng.");
        }

        if (string.Equals(room.Status, "closed", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationResult.Failed("ROOM_CLOSED", "Phòng đã đóng.");
        }

        var isMember = await _dbContext.RoomMemberships
            .AsNoTracking()
            .AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.LeftAt == null, cancellationToken);

        if (!isMember)
        {
            return AuthorizationResult.Failed("NOT_ROOM_MEMBER", "Chỉ thành viên trong phòng mới được phép tham gia hoặc gửi tín hiệu video.");
        }

        return AuthorizationResult.Succeeded();
    }
}
