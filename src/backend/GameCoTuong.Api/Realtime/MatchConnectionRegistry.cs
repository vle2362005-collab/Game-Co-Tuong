namespace GameCoTuong.Api.Realtime;

public sealed class MatchConnectionRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, (Guid MatchId, Guid UserId)> byConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid MatchId, Guid UserId), HashSet<string>> byPlayer = [];
    private readonly Dictionary<(Guid MatchId, Guid UserId), PlayerTransitionGate> transitionGates = [];

    public async Task<IDisposable> AcquirePlayerTransitionAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var playerKey = (matchId, userId);
        PlayerTransitionGate transitionGate;
        lock (gate)
        {
            if (transitionGates.TryGetValue(playerKey, out var existingGate))
            {
                transitionGate = existingGate;
            }
            else
            {
                transitionGate = new PlayerTransitionGate();
                transitionGates.Add(playerKey, transitionGate);
            }

            transitionGate.ReferenceCount++;
        }

        try
        {
            await transitionGate.Semaphore.WaitAsync(cancellationToken);
            return new PlayerTransitionLease(this, playerKey, transitionGate);
        }
        catch (OperationCanceledException)
        {
            ReleaseReference(playerKey, transitionGate);
            throw;
        }
    }

    public bool HasConnections(Guid matchId, Guid userId)
    {
        lock (gate)
        {
            return byPlayer.TryGetValue((matchId, userId), out var connections)
                && connections.Count > 0;
        }
    }

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

    private void ReleaseTransition(
        (Guid MatchId, Guid UserId) playerKey,
        PlayerTransitionGate transitionGate)
    {
        transitionGate.Semaphore.Release();
        ReleaseReference(playerKey, transitionGate);
    }

    private void ReleaseReference(
        (Guid MatchId, Guid UserId) playerKey,
        PlayerTransitionGate transitionGate)
    {
        lock (gate)
        {
            transitionGate.ReferenceCount--;
            if (transitionGate.ReferenceCount == 0)
            {
                transitionGates.Remove(playerKey);
            }
        }
    }

    private sealed class PlayerTransitionGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class PlayerTransitionLease(
        MatchConnectionRegistry registry,
        (Guid MatchId, Guid UserId) playerKey,
        PlayerTransitionGate transitionGate) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            registry.ReleaseTransition(playerKey, transitionGate);
        }
    }
}
