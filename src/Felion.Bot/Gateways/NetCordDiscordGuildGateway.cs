using System.Net;
using Felion.Application.Discord;
using Felion.Bot.Configuration;
using NetCord.Rest;

namespace Felion.Bot.Gateways;

public sealed class NetCordDiscordGuildGateway(
    RestClient restClient,
    ConfiguredDiscordGuild configuredGuild) : IDiscordGuildGateway
{
    public async Task KickUserAsync(
        long discordUserId,
        CancellationToken cancellationToken)
    {
        if (discordUserId <= 0)
        {
            throw new DiscordGuildGatewayException("Discord user ID must be positive.");
        }

        try
        {
            await restClient.KickGuildUserAsync(
                configuredGuild.Id,
                (ulong)discordUserId,
                cancellationToken: cancellationToken);
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // A user who already left the guild is already in the desired state.
        }
        catch (RestException)
        {
            throw new DiscordGuildGatewayException("Discord rejected the guild member kick request.");
        }
    }
}
