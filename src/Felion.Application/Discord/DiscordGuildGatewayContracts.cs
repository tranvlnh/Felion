namespace Felion.Application.Discord;

public interface IDiscordGuildGateway
{
    public Task KickUserAsync(
        long discordUserId,
        CancellationToken cancellationToken);
}

public class DiscordGuildGatewayException(string message) : Exception(message);
