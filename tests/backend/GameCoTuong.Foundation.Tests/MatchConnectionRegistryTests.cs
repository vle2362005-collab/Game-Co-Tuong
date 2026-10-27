using GameCoTuong.Api.Reconnect;

namespace GameCoTuong.Foundation.Tests;

public sealed class MatchConnectionRegistryTests
{
    [Fact]
    public void Disconnecting_one_of_multiple_connections_keeps_the_player_connected()
    {
        var registry = new MatchConnectionRegistry();
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        registry.Add(matchId, userId, "first");
        registry.Add(matchId, userId, "second");

        Assert.False(registry.Remove(matchId, userId, "first"));
        Assert.True(registry.Remove(matchId, userId, "second"));
    }

    [Fact]
    public void Disconnecting_the_last_connection_reports_offline()
    {
        var registry = new MatchConnectionRegistry();
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        registry.Add(matchId, userId, "only");

        Assert.True(registry.Remove(matchId, userId, "only"));
        Assert.False(registry.Remove(matchId, userId, "only"));
    }
}
