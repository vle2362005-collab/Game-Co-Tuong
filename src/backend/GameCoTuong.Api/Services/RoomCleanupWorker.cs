using GameCoTuong.Infrastructure.Persistence;

namespace GameCoTuong.Api.Services;

internal sealed class RoomCleanupWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RoomCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<RoomCleanupService>();
                var closedRooms = await cleanupService.CleanupExpiredRoomsAsync(DateTimeOffset.UtcNow, stoppingToken);

                if (closedRooms > 0)
                {
                    logger.LogInformation("Closed {RoomCount} expired rooms.", closedRooms);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Room cleanup failed; the worker will retry on its next interval.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
