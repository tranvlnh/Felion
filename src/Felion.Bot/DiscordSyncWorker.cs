using Felion.Application.Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Felion.Bot;

public sealed partial class DiscordSyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DiscordSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDiscordSyncProcessor>();
                var processed = await processor.ProcessNextAsync(stoppingToken);
                if (!processed)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogWorkerIterationFailed(logger, exception);
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Error,
        Message = "Discord synchronization worker iteration failed.")]
    private static partial void LogWorkerIterationFailed(
        ILogger logger,
        Exception exception);
}
