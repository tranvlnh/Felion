using System.Net;
using Felion.Application.Discord;
using Felion.Bot.Components.Verification;
using NetCord.Rest;

namespace Felion.Bot.Gateways;

public sealed class NetCordDiscordVerificationMessageGateway(RestClient restClient)
    : IDiscordVerificationMessageGateway
{
    public async Task<DiscordVerificationMessageSnapshot> PublishAsync(
        long channelId,
        CancellationToken cancellationToken)
    {
        if (channelId <= 0)
        {
            throw new DiscordVerificationMessageValidationException(
                "Discord channel ID must be a positive signed snowflake.");
        }

        try
        {
            var message = await restClient.SendMessageAsync(
                (ulong)channelId,
                VerificationMessageFactory.Create(),
                cancellationToken: cancellationToken);
            if (message.Id > long.MaxValue)
            {
                throw new DiscordVerificationMessageGatewayException(
                    "The Discord message ID is outside the supported signed bigint range.");
            }

            return new DiscordVerificationMessageSnapshot(channelId, (long)message.Id);
        }
        catch (RestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new DiscordVerificationMessageGatewayException(
                "The selected Discord channel was not found or is unavailable to the bot.");
        }
        catch (RestException)
        {
            throw new DiscordVerificationMessageGatewayException(
                "Discord rejected the verification message request.");
        }
    }
}
