namespace GameCoTuong.Api.Realtime;

public sealed class MatchConnectionRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, (Guid MatchId, Guid UserId)> byConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid MatchId, Guid UserId), HashSet<string>> byPlayer = [];

    public bool CanTrack(string connectionId, Guid matchId, Guid userId)
    {
        lock (gate)
        {
            return !byConnection.TryGetValue(connectionId, out var current)
                || current == (matchId, userId);
        }
    }

    public void Track(string connectionId, Guid matchId, Guid userId)
    {
        lock (gate)
        {
            if (byConnection.TryGetValue(connectionId, out var current))
            {
                if (current == (matchId, userId))
                {
                    return;
                }

                throw new InvalidOperationException("Một kết nối chỉ được tham gia một trận đấu.");
            }

            var playerKey = (matchId, userId);
            if (!byPlayer.TryGetValue(playerKey, out var connections))
            {
                connections = new HashSet<string>(StringComparer.Ordinal);
                byPlayer.Add(playerKey, connections);
            }

            connections.Add(connectionId);
            byConnection.Add(connectionId, playerKey);
        }
    }

    public bool UntrackLastConnection(string connectionId, out Guid matchId, out Guid userId)
    {
        lock (gate)
        {
            if (!byConnection.Remove(connectionId, out var playerKey))
            {
                matchId = Guid.Empty;
                userId = Guid.Empty;
                return false;
            }

            var connections = byPlayer[playerKey];
            connections.Remove(connectionId);
            if (connections.Count > 0)
            {
                matchId = Guid.Empty;
                userId = Guid.Empty;
                return false;
            }

            byPlayer.Remove(playerKey);
            matchId = playerKey.MatchId;
            userId = playerKey.UserId;
            return true;
        }
    }
}
