using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Services.ApplicationCommands;

namespace Felion.Bot;

public sealed partial class DiscordCommandRegistrationWorker(
    GatewayClient gatewayClient,
    ApplicationCommandServiceManager commandServiceManager,
    ConfiguredDiscordGuild configuredGuild,
    ILogger<DiscordCommandRegistrationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (gatewayClient.Status != WebSocketStatus.Ready)
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                    continue;
                }

                await commandServiceManager.RegisterCommandsAsync(
                    gatewayClient.Rest,
                    gatewayClient.Id,
                    configuredGuild.Id,
                    cancellationToken: stoppingToken);
                LogCommandsRegistered(logger, configuredGuild.Id);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogCommandRegistrationFailed(logger, exception);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "Discord application commands registered for guild {GuildId}.")]
    private static partial void LogCommandsRegistered(ILogger logger, ulong guildId);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Error,
        Message = "Discord application command registration failed.")]
    private static partial void LogCommandRegistrationFailed(ILogger logger, Exception exception);
}
