using GameCoTuong.Api.Realtime;

namespace GameCoTuong.Foundation.Tests;

public sealed class MatchReconnectTests
{
    [Fact]
    public void Reconnect_time_is_carried_over_between_disconnects()
    {
        var firstDisconnect = DateTimeOffset.Parse("2026-10-27T10:00:00Z");
        var firstDeadline = ReconnectWindow.StartDeadline(firstDisconnect, MatchReconnectService.RecoveryWindowMs);
        var firstReconnect = firstDisconnect.AddSeconds(23);
        var remainingAfterFirstReconnect = ReconnectWindow.GetRemainingMilliseconds(firstDeadline, firstReconnect);

        var secondDeadline = ReconnectWindow.StartDeadline(firstReconnect, remainingAfterFirstReconnect);
        var secondReconnect = firstReconnect.AddSeconds(10);

        Assert.Equal(37_000, remainingAfterFirstReconnect);
        Assert.Equal(27_000, ReconnectWindow.GetRemainingMilliseconds(secondDeadline, secondReconnect));
    }

    [Fact]
    public void Reconnect_window_never_exceeds_sixty_seconds_or_becomes_negative()
    {
        var now = DateTimeOffset.Parse("2026-10-27T10:00:00Z");

        Assert.Equal(now.AddSeconds(60), ReconnectWindow.StartDeadline(now, 90_000));
        Assert.Equal(now, ReconnectWindow.StartDeadline(now, -1));
        Assert.Equal(0, ReconnectWindow.GetRemainingMilliseconds(now.AddSeconds(-1), now));
    }

    [Fact]
    public void Expired_player_loses_only_when_opponent_is_connected()
    {
        var decision = MatchReconnectExpiryPolicy.Decide(
            redExpired: true,
            blackExpired: false,
            redDisconnected: true,
            blackDisconnected: false);

        Assert.NotNull(decision);
        Assert.Equal("ended", decision.Status);
        Assert.Equal("black", decision.Winner);
        Assert.Equal("disconnect_timeout", decision.Reason);
    }

    [Fact]
    public void Both_players_expiring_cancels_match_without_winner()
    {
        var decision = MatchReconnectExpiryPolicy.Decide(
            redExpired: true,
            blackExpired: true,
            redDisconnected: true,
            blackDisconnected: true);

        Assert.NotNull(decision);
        Assert.Equal("cancelled", decision.Status);
        Assert.Null(decision.Winner);
        Assert.Equal("both_disconnected", decision.Reason);
    }

    [Fact]
    public void Expired_player_does_not_win_or_end_match_while_opponent_is_still_disconnected()
    {
        var decision = MatchReconnectExpiryPolicy.Decide(
            redExpired: true,
            blackExpired: false,
            redDisconnected: true,
            blackDisconnected: true);

        Assert.Null(decision);
    }

    [Fact]
    public void Disconnecting_one_of_multiple_connections_does_not_mark_player_offline()
    {
        var registry = new MatchConnectionRegistry();
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        registry.Track("connection-1", matchId, userId);
        registry.Track("connection-2", matchId, userId);

        Assert.False(registry.UntrackLastConnection("connection-1", out _, out _));
        Assert.True(registry.UntrackLastConnection("connection-2", out var disconnectedMatchId, out var disconnectedUserId));
        Assert.Equal(matchId, disconnectedMatchId);
        Assert.Equal(userId, disconnectedUserId);
    }

    [Fact]
    public async Task Player_transitions_are_serialized_per_match_and_user()
    {
        var registry = new MatchConnectionRegistry();
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var first = await registry.AcquirePlayerTransitionAsync(
            matchId, userId, CancellationToken.None);

        var secondTask = registry.AcquirePlayerTransitionAsync(
            matchId, userId, CancellationToken.None);

        Assert.False(secondTask.IsCompleted);

        first.Dispose();
        using var second = await secondTask;
    }

    [Fact]
    public async Task New_connection_is_visible_when_old_disconnect_rechecks_presence()
    {
        var registry = new MatchConnectionRegistry();
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        registry.Track("old-connection", matchId, userId);
        Assert.True(registry.UntrackLastConnection("old-connection", out _, out _));

        using (var reconnectTransition = await registry.AcquirePlayerTransitionAsync(
            matchId, userId, CancellationToken.None))
        {
            registry.Track("new-connection", matchId, userId);
        }

        using var oldDisconnectTransition = await registry.AcquirePlayerTransitionAsync(
            matchId, userId, CancellationToken.None);

        Assert.True(registry.HasConnections(matchId, userId));
    }
}
