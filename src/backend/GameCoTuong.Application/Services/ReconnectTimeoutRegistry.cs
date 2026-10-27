using System.Collections.Concurrent;

namespace GameCoTuong.Application.Services;

public enum ReconnectTimeoutScope
{
    Room,
    Match
}

public readonly record struct ReconnectTimeout(
    ReconnectTimeoutScope Scope,
    Guid EntityId,
    string Participant,
    DateTimeOffset Deadline);

public sealed class ReconnectTimeoutRegistry
{
    private readonly ConcurrentDictionary<(ReconnectTimeoutScope Scope, Guid EntityId, string Participant), ReconnectTimeout> timeouts = new();

    public void Schedule(ReconnectTimeoutScope scope, Guid entityId, string participant, DateTimeOffset deadline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participant);
        timeouts[(scope, entityId, participant)] = new ReconnectTimeout(scope, entityId, participant, deadline);
    }

    public bool Cancel(ReconnectTimeoutScope scope, Guid entityId, string participant) =>
        timeouts.TryRemove((scope, entityId, participant), out _);

    public void Clear(ReconnectTimeoutScope scope, Guid entityId)
    {
        foreach (var key in timeouts.Keys.Where(key => key.Scope == scope && key.EntityId == entityId))
        {
            timeouts.TryRemove(key, out _);
        }
    }

    public IReadOnlyList<ReconnectTimeout> GetExpired(DateTimeOffset now) =>
        timeouts.Values
            .Where(timeout => now >= timeout.Deadline)
            .OrderBy(timeout => timeout.Deadline)
            .ToArray();
}
