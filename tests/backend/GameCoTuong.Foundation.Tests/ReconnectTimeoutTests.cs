using GameCoTuong.Application.Services;
using GameCoTuong.Domain.Entities;

namespace GameCoTuong.Foundation.Tests;

public sealed class ReconnectTimeoutTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Guest_timeout_releases_seat_at_deadline()
    {
        var (room, _, guest) = CreateRoom();
        var service = CreateService();

        Assert.True(service.MarkDisconnected(room, guest, Start));
        service.ProcessRoomTimeouts([room], Start.AddMilliseconds(59_999));

        Assert.Null(guest.LeftAt);
        Assert.Equal("waiting", room.Status);

        service.ProcessRoomTimeouts([room], Start.AddSeconds(60));

        Assert.Equal(Start.AddSeconds(60), guest.LeftAt);
        Assert.Null(guest.ReconnectDeadlineAt);
        Assert.False(guest.IsConnected);
        Assert.False(guest.IsReady);
        Assert.Equal("waiting", room.Status);
    }

    [Fact]
    public void Host_timeout_closes_room_at_deadline()
    {
        var (room, host, _) = CreateRoom();
        var service = CreateService();

        service.MarkDisconnected(room, host, Start);
        service.ProcessRoomTimeouts([room], Start.AddSeconds(60));

        Assert.Equal("closed", room.Status);
        Assert.Equal(Start.AddSeconds(60), room.ClosedAt);
    }

    [Fact]
    public void Expired_host_closes_room_even_if_guest_expires_at_the_same_time()
    {
        var (room, host, guest) = CreateRoom();
        var service = CreateService();
        service.MarkDisconnected(room, host, Start);
        service.MarkDisconnected(room, guest, Start);

        service.ProcessRoomTimeouts([room], Start.AddSeconds(60));

        Assert.Equal("closed", room.Status);
        Assert.Null(guest.LeftAt);
    }

    [Fact]
    public void Room_reconnect_before_deadline_succeeds_but_at_deadline_releases_guest()
    {
        var (room, _, guest) = CreateRoom();
        var service = CreateService();

        service.MarkDisconnected(room, guest, Start);

        Assert.True(service.TryReconnect(room, guest, Start.AddMilliseconds(59_999)));
        Assert.True(guest.IsConnected);
        Assert.Null(guest.ReconnectDeadlineAt);

        service.MarkDisconnected(room, guest, Start.AddMinutes(2));
        Assert.False(service.TryReconnect(room, guest, Start.AddMinutes(3)));
        Assert.NotNull(guest.LeftAt);
    }

    [Fact]
    public void Reconnect_budget_is_shared_across_disconnects_and_match_resumes_when_both_return()
    {
        var match = CreateMatch();
        var service = CreateService();

        Assert.True(service.MarkDisconnected(match, "red", Start));
        Assert.Equal("paused", match.Status);
        Assert.Null(match.TurnStartedAt);
        Assert.True(service.TryReconnect(match, "red", Start.AddSeconds(20)));
        Assert.Equal(40_000, match.RedRecoveryRemainingMs);
        Assert.Equal("playing", match.Status);

        Assert.True(service.MarkDisconnected(match, "red", Start.AddSeconds(30)));
        Assert.Equal(Start.AddSeconds(70), match.RedDeadlineAt);
        Assert.True(service.TryReconnect(match, "red", Start.AddSeconds(50)));

        Assert.Equal(20_000, match.RedRecoveryRemainingMs);
        Assert.Equal("playing", match.Status);
        Assert.Null(match.RedDisconnectedAt);
        Assert.Null(match.RedDeadlineAt);
    }

    [Fact]
    public void Reconnect_at_deadline_loses_when_opponent_is_online()
    {
        var match = CreateMatch();
        var service = CreateService();
        service.MarkDisconnected(match, "red", Start);

        Assert.False(service.TryReconnect(match, "red", Start.AddSeconds(60)));

        Assert.Equal("finished", match.Status);
        Assert.Equal("black", match.Winner);
        Assert.Equal("reconnectTimeout", match.ResultReason);
        Assert.Equal(Start.AddSeconds(60), match.EndedAt);
    }

    [Fact]
    public void Reconnect_one_millisecond_before_deadline_succeeds_with_one_millisecond_left()
    {
        var match = CreateMatch();
        var service = CreateService();
        service.MarkDisconnected(match, "red", Start);

        Assert.True(service.TryReconnect(match, "red", Start.AddMilliseconds(59_999)));
        Assert.Equal(1, match.RedRecoveryRemainingMs);
        Assert.Equal("playing", match.Status);
    }

    [Fact]
    public void Match_clock_is_charged_until_disconnect_then_stays_paused_offline()
    {
        var match = CreateMatch();
        var service = CreateService();

        service.MarkDisconnected(match, "red", Start.AddSeconds(10));

        Assert.Equal(290_000, match.RedRemainingMs);
        Assert.Null(match.TurnStartedAt);
        Assert.True(service.TryReconnect(match, "red", Start.AddSeconds(50)));
        Assert.Equal(290_000, match.RedRemainingMs);
        Assert.Equal(Start.AddSeconds(50), match.TurnStartedAt);
    }

    [Fact]
    public void First_timeout_waits_while_both_offline_and_second_timeout_cancels_match()
    {
        var match = CreateMatch();
        var service = CreateService();
        service.MarkDisconnected(match, "red", Start);
        service.MarkDisconnected(match, "black", Start.AddSeconds(10));

        service.ProcessMatchTimeouts([match], Start.AddSeconds(60));
        Assert.Equal("paused", match.Status);
        Assert.Null(match.Winner);
        Assert.Null(match.EndedAt);

        service.ProcessMatchTimeouts([match], Start.AddSeconds(70));

        Assert.Equal("cancelled", match.Status);
        Assert.Null(match.Winner);
        Assert.Equal("bothReconnectTimeout", match.ResultReason);
        Assert.Equal(Start.AddSeconds(70), match.EndedAt);
    }

    [Fact]
    public void Reconnecting_player_wins_when_opponent_has_already_expired()
    {
        var match = CreateMatch();
        var service = CreateService();
        service.MarkDisconnected(match, "red", Start);
        service.MarkDisconnected(match, "black", Start.AddSeconds(10));

        Assert.True(service.TryReconnect(match, "black", Start.AddSeconds(20)));
        service.ProcessMatchTimeouts([match], Start.AddSeconds(60));

        Assert.Equal("finished", match.Status);
        Assert.Equal("black", match.Winner);
        Assert.Equal("reconnectTimeout", match.ResultReason);
    }

    [Fact]
    public void Simultaneously_expired_players_cancel_instead_of_awarding_a_win()
    {
        var match = CreateMatch();
        var service = CreateService();
        service.MarkDisconnected(match, "red", Start);
        service.MarkDisconnected(match, "black", Start);

        service.ProcessMatchTimeouts([match], Start.AddSeconds(60));

        Assert.Equal("cancelled", match.Status);
        Assert.Null(match.Winner);
        Assert.Equal("bothReconnectTimeout", match.ResultReason);
    }

    private static MatchReconnectService CreateService() =>
        new(new ReconnectTimeoutRegistry());

    private static Match CreateMatch() => new()
    {
        Id = Guid.NewGuid(),
        Status = "playing",
        SideToMove = "red",
        RedRemainingMs = 300_000,
        BlackRemainingMs = 300_000,
        TurnStartedAt = Start
    };

    private static (Room Room, RoomMembership Host, RoomMembership Guest) CreateRoom()
    {
        var roomId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var host = new RoomMembership
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            UserId = hostId,
            Side = "red",
            IsReady = true
        };
        var guest = new RoomMembership
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            UserId = Guid.NewGuid(),
            Side = "black",
            IsReady = true
        };
        var room = new Room
        {
            Id = roomId,
            HostUserId = hostId,
            Status = "waiting",
            Memberships = [host, guest]
        };

        return (room, host, guest);
    }
}
