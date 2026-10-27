namespace GameCoTuong.Api.Reconnect;

public sealed class MatchConnectionRegistry
{
    private readonly object syncRoot = new();
    private readonly Dictionary<(Guid MatchId, Guid UserId), HashSet<string>> connections = [];
    private readonly Dictionary<(Guid MatchId, Guid UserId), SemaphoreSlim> locks = [];

    public async ValueTask<IDisposable> AcquireAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim gate;
        lock (syncRoot)
        {
            var key = (matchId, userId);
            if (locks.TryGetValue(key, out var existingGate))
            {
                gate = existingGate;
            }
            else
            {
                gate = new SemaphoreSlim(1, 1);
                locks.Add(key, gate);
            }
        }

        await gate.WaitAsync(cancellationToken);
        return new GateLease(gate);
    }

    public void Add(Guid matchId, Guid userId, string connectionId)
    {
        lock (syncRoot)
        {
            var key = (matchId, userId);
            if (!connections.TryGetValue(key, out var userConnections))
            {
                userConnections = [];
                connections.Add(key, userConnections);
            }

            userConnections.Add(connectionId);
        }
    }

    public bool Remove(Guid matchId, Guid userId, string connectionId)
    {
        lock (syncRoot)
        {
            var key = (matchId, userId);
            if (!connections.TryGetValue(key, out var userConnections))
            {
                return false;
            }

            userConnections.Remove(connectionId);
            if (userConnections.Count > 0)
            {
                return false;
            }

            connections.Remove(key);
            return true;
        }
    }

    private sealed class GateLease(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
