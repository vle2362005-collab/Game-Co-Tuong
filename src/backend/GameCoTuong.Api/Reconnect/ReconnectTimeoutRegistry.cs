using GameCoTuong.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Reconnect;

public sealed class ReconnectTimeoutRegistry : IDisposable
{
    private readonly Dictionary<(Guid MatchId, Guid UserId), Entry> entries = [];
    private readonly object syncRoot = new();
    private readonly TimeProvider timeProvider;
    private readonly IHubContext<MatchHub> hubContext;
    private readonly ILogger<ReconnectTimeoutRegistry> logger;

    public ReconnectTimeoutRegistry(
        IHubContext<MatchHub> hubContext,
        ILogger<ReconnectTimeoutRegistry> logger,
        TimeProvider timeProvider)
    {
        this.hubContext = hubContext;
        this.logger = logger;
        this.timeProvider = timeProvider;
    }

    public void Schedule(Guid matchId, Guid userId, DateTimeOffset deadline)
    {
        var key = (matchId, userId);
        var entry = new Entry(new CancellationTokenSource());
        Entry? previous;
        lock (syncRoot)
        {
            entries.Remove(key, out previous);
            entries.Add(key, entry);
        }
        previous?.Cancellation.Cancel();
        _ = NotifyOnExpiryAsync(key, entry, deadline);
    }

    public void Cancel(Guid matchId, Guid userId)
    {
        Entry? entry;
        lock (syncRoot)
        {
            entries.Remove((matchId, userId), out entry);
        }

        entry?.Cancellation.Cancel();
    }

    private async Task NotifyOnExpiryAsync(
        (Guid MatchId, Guid UserId) key,
        Entry entry,
        DateTimeOffset deadline)
    {
        try
        {
            var delay = deadline - timeProvider.GetUtcNow();
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, timeProvider, entry.Cancellation.Token);
            }

            await hubContext.Clients.Group(MatchHub.GetMatchGroup(key.MatchId))
                .SendAsync("MatchReconnectExpired", new { key.MatchId, key.UserId });
        }
        catch (OperationCanceledException) when (entry.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to publish reconnect expiry for match {MatchId} and user {UserId}.",
                key.MatchId, key.UserId);
        }
        finally
        {
            lock (syncRoot)
            {
                if (entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                {
                    entries.Remove(key);
                }
            }

            entry.Cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        Entry[] pending;
        lock (syncRoot)
        {
            pending = entries.Values.ToArray();
            entries.Clear();
        }

        foreach (var entry in pending)
        {
            entry.Cancellation.Cancel();
        }
    }

    private sealed record Entry(CancellationTokenSource Cancellation);
}
