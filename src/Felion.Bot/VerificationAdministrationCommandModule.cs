using Felion.Application.Discord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Felion.Bot;

[SlashCommand("verification", "Manage Felion account verification")]
public sealed class VerificationAdministrationCommandModule(
    IDiscordAuthorizationService authorizationService,
    IDiscordVerificationMessageService verificationMessageService,
    ConfiguredDiscordGuild configuredGuild)
    : AdministrationCommandModuleBase(authorizationService, configuredGuild)
{
    [SubSlashCommand("publish", "Publish the verification message in the current channel")]
    public async Task<InteractionCallbackProperties> PublishAsync()
    {
        if (!IsConfiguredGuild())
        {
            return AdministrationCommandResponses.Error(
                "This command is only available in the configured Felion guild.");
        }

        if (!TryGetDiscordUserId(out var discordUserId))
        {
            return AdministrationCommandResponses.Error("The Discord user ID is not supported.");
        }

        var rawChannelId = Context.Channel.Id;
        if (rawChannelId == 0 || rawChannelId > (ulong)long.MaxValue)
        {
            return AdministrationCommandResponses.Error("The Discord channel ID is not supported.");
        }

        try
        {
            var message = await verificationMessageService.PublishFromDiscordAsync(
                discordUserId,
                (long)rawChannelId,
                CorrelationId(),
                CancellationToken.None);

            return AdministrationCommandResponses.Success(
                $"✅ Verification message đã được đăng tại <#{message.ChannelId}> (message `{message.MessageId}`).");
        }
        catch (Exception exception) when (exception is DiscordAuthorizationException
            or DiscordVerificationMessageAccessDeniedException
            or DiscordVerificationMessageValidationException
            or DiscordVerificationMessageGatewayException
            or DiscordGuildPermissionGatewayException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }
}
