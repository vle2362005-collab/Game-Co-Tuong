using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace GameCoTuong.Api.Realtime;

public sealed class ReconnectDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    IHubContext<MatchHub> hubContext,
    ILogger<ReconnectDeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<MatchReconnectService>();
                var expiredMatchIds = await service.FindExpiredMatchIdsAsync(stoppingToken);
                foreach (var matchId in expiredMatchIds)
                {
                    var state = await service.ProcessExpiredAsync(matchId, stoppingToken);
                    if (state is not null)
                    {
                        logger.LogInformation("Match {MatchId} ended with result {ResultReason}.",
                            matchId, state.ResultReason);
                        await hubContext.Clients.Group(MatchHub.GroupName(matchId))
                            .SendAsync("MatchEnded", state, stoppingToken);
                    }
                }
            }
            catch (DbUpdateException error)
            {
                logger.LogError(error, "Failed to persist expired match recovery state; retrying next interval.");
            }
            catch (DbException error)
            {
                logger.LogError(error, "Database unavailable while processing reconnect deadlines; retrying next interval.");
            }
        }
    }
}
