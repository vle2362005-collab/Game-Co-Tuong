using GameCoTuong.Application.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameCoTuong.Infrastructure.Game;

public sealed class MatchRecoveryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<MatchRecoveryHostedService> logger,
    IConfiguration configuration,
    IHostEnvironment environment) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Automated recovery is for the SQL Server demo store; SQLite is used by tests.
        if (environment.IsEnvironment("Testing") || configuration["Database:Provider"] == "Sqlite")
            return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var recovered = await scope.ServiceProvider.GetRequiredService<GameService>().RecoverAfterRestart();
        logger.LogInformation("Game recovery service started. {MatchCount} {ServiceState}", recovered, "running");
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var game = scope.ServiceProvider.GetRequiredService<GameService>();
            var expired = await game.ExpireRecoveries();
            if (expired > 0)
                logger.LogInformation("Expired match recoveries processed. {MatchCount} {ServiceState}", expired, "running");
            var expiredDrawOffers = await game.ExpireDrawOffers();
            if (expiredDrawOffers.Count > 0)
            {
                var notifier = scope.ServiceProvider.GetRequiredService<IDrawOfferExpirationNotifier>();
                foreach (var offer in expiredDrawOffers)
                    await notifier.NotifyExpired(offer.MatchId, offer.RoomId, stoppingToken);
                logger.LogInformation("Expired draw offers processed. {MatchCount} {ServiceState}", expiredDrawOffers.Count, "running");
            }
        }
    }
}
