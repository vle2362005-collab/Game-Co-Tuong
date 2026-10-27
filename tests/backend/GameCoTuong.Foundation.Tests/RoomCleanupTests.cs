using GameCoTuong.Domain.Entities;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Foundation.Tests;

public sealed class RoomCleanupTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Closes_idle_waiting_rooms_and_clears_temporary_membership_state()
    {
        await using var dbContext = CreateDbContext();
        var expiredRoom = CreateRoom("waiting", Now.AddMinutes(-16));
        var expiredMembership = CreateMembership(expiredRoom, Now.AddMinutes(-1));
        var activeMembership = CreateMembership(expiredRoom, null);
        activeMembership.IsReady = true;
        var reconnectRoom = CreateRoom("waiting", Now.AddMinutes(-30));
        var reconnectMembership = CreateMembership(reconnectRoom, Now.AddMinutes(2));
        dbContext.Rooms.AddRange(expiredRoom, reconnectRoom);
        await dbContext.SaveChangesAsync();

        var closedCount = await new RoomCleanupService(dbContext).CleanupExpiredRoomsAsync(Now);

        Assert.Equal(1, closedCount);
        Assert.Equal("closed", expiredRoom.Status);
        Assert.Equal(Now, expiredRoom.ClosedAt);
        Assert.False(expiredMembership.IsConnected);
        Assert.False(expiredMembership.IsReady);
        Assert.Null(expiredMembership.ReconnectDeadlineAt);
        Assert.Equal(Now, expiredMembership.LeftAt);
        Assert.False(activeMembership.IsConnected);
        Assert.False(activeMembership.IsReady);
        Assert.Null(activeMembership.ReconnectDeadlineAt);
        Assert.Equal(Now, activeMembership.LeftAt);

        Assert.Equal("waiting", reconnectRoom.Status);
        Assert.Null(reconnectRoom.ClosedAt);
        Assert.True(reconnectMembership.IsConnected);
        Assert.Equal(Now.AddMinutes(2), reconnectMembership.ReconnectDeadlineAt);
    }

    [Fact]
    public async Task Does_not_close_playing_rooms_or_waiting_rooms_with_a_live_reconnect_window()
    {
        await using var dbContext = CreateDbContext();
        var playingRoom = CreateRoom("playing", Now.AddDays(-1));
        var reconnectRoom = CreateRoom("waiting", Now.AddMinutes(-16));
        var reconnectMembership = CreateMembership(reconnectRoom, Now.AddSeconds(1));
        dbContext.Rooms.AddRange(playingRoom, reconnectRoom);
        await dbContext.SaveChangesAsync();

        var closedCount = await new RoomCleanupService(dbContext).CleanupExpiredRoomsAsync(Now);

        Assert.Equal(0, closedCount);
        Assert.Equal("playing", playingRoom.Status);
        Assert.Equal("waiting", reconnectRoom.Status);
        Assert.True(reconnectMembership.IsConnected);
    }

    [Fact]
    public async Task Closes_finished_rooms_after_five_minutes_or_when_both_players_have_left()
    {
        await using var dbContext = CreateDbContext();
        var retainedRoom = CreateFinishedRoom(Now.AddMinutes(-2));
        CreateMembership(retainedRoom, null);
        CreateMembership(retainedRoom, null);

        var expiredRoom = CreateFinishedRoom(Now.AddMinutes(-6));
        CreateMembership(expiredRoom, null);
        CreateMembership(expiredRoom, null);

        var bothLeftRoom = CreateFinishedRoom(Now.AddMinutes(-1));
        CreateMembership(bothLeftRoom, null, Now.AddSeconds(-30));
        CreateMembership(bothLeftRoom, null, Now.AddSeconds(-20));

        var oldFinishedRoomWithoutCurrentMatch = CreateRoom("finished", Now.AddMinutes(-6));

        dbContext.Rooms.AddRange(retainedRoom, expiredRoom, bothLeftRoom, oldFinishedRoomWithoutCurrentMatch);
        await dbContext.SaveChangesAsync();

        var closedCount = await new RoomCleanupService(dbContext).CleanupExpiredRoomsAsync(Now);

        Assert.Equal(3, closedCount);
        Assert.Equal("finished", retainedRoom.Status);
        Assert.Null(retainedRoom.ClosedAt);
        Assert.Equal("closed", expiredRoom.Status);
        Assert.Equal(Now, expiredRoom.ClosedAt);
        Assert.Equal("closed", bothLeftRoom.Status);
        Assert.Equal(Now, bothLeftRoom.ClosedAt);
        Assert.Equal("closed", oldFinishedRoomWithoutCurrentMatch.Status);
    }

    [Fact]
    public async Task Cleanup_is_idempotent_and_preserves_match_and_move_history()
    {
        await using var dbContext = CreateDbContext();
        var room = CreateFinishedRoom(Now.AddMinutes(-10));
        var match = room.CurrentMatch!;
        var move = new Move
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            Match = match,
            Ply = 1,
            RequestId = Guid.NewGuid(),
            MatchVersion = 2,
            PieceId = "red-soldier-1",
            CreatedAt = Now.AddMinutes(-9)
        };
        match.Moves.Add(move);
        CreateMembership(room, null);
        CreateMembership(room, null);
        dbContext.Rooms.Add(room);
        await dbContext.SaveChangesAsync();

        var cleanupService = new RoomCleanupService(dbContext);
        var firstRunClosedCount = await cleanupService.CleanupExpiredRoomsAsync(Now);
        var secondRunClosedCount = await cleanupService.CleanupExpiredRoomsAsync(Now.AddMinutes(1));

        Assert.Equal(1, firstRunClosedCount);
        Assert.Equal(0, secondRunClosedCount);
        Assert.Equal("closed", room.Status);
        Assert.Equal(1, await dbContext.Matches.CountAsync());
        Assert.Equal(1, await dbContext.Moves.CountAsync());
        Assert.Equal(match.Id, (await dbContext.Moves.SingleAsync()).MatchId);
    }

    private static GameDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GameDbContext(options);
    }

    private static Room CreateRoom(string status, DateTimeOffset lastActivityAt) => new()
    {
        Id = Guid.NewGuid(),
        Code = Guid.NewGuid().ToString("N")[..6],
        Name = "Test room",
        HostUserId = Guid.NewGuid(),
        Status = status,
        LastActivityAt = lastActivityAt,
        CreatedAt = lastActivityAt
    };

    private static Room CreateFinishedRoom(DateTimeOffset endedAt)
    {
        var room = CreateRoom("finished", endedAt);
        var match = new Match
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            Status = "finished",
            StartedAt = endedAt.AddMinutes(-10),
            EndedAt = endedAt
        };
        room.CurrentMatchId = match.Id;
        room.CurrentMatch = match;
        room.Matches.Add(match);
        return room;
    }

    private static RoomMembership CreateMembership(
        Room room,
        DateTimeOffset? reconnectDeadlineAt,
        DateTimeOffset? leftAt = null)
    {
        var membership = new RoomMembership
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            UserId = Guid.NewGuid(),
            ReconnectDeadlineAt = reconnectDeadlineAt,
            JoinedAt = Now.AddHours(-1),
            LeftAt = leftAt
        };
        room.Memberships.Add(membership);
        return membership;
    }
}
