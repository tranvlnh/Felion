using System.Globalization;
using Felion.Application.Discord;
using Felion.Application.Members;
using NetCord.Services.ApplicationCommands;

namespace Felion.Bot;

public abstract class AdministrationCommandModuleBase(
    IDiscordAuthorizationService authorizationService,
    ConfiguredDiscordGuild configuredGuild) : ApplicationCommandModule<ApplicationCommandContext>
{
    protected IDiscordAuthorizationService AuthorizationService { get; } = authorizationService;

    protected ConfiguredDiscordGuild ConfiguredGuild { get; } = configuredGuild;

    protected bool IsConfiguredGuild()
    {
        return ConfiguredGuild.Matches(Context.Interaction.GuildId);
    }

    protected bool TryGetDiscordUserId(out long discordUserId)
    {
        var rawUserId = Context.Interaction.User.Id;
        if (rawUserId == 0 || rawUserId > (ulong)long.MaxValue)
        {
            discordUserId = 0;
            return false;
        }

        discordUserId = (long)rawUserId;
        return true;
    }

    protected string CorrelationId()
    {
        return $"discord-command-{Context.Interaction.Id.ToString(CultureInfo.InvariantCulture)}";
    }

    protected static string FormatReferenceDataError(Exception exception)
    {
        return exception switch
        {
            ReferenceDataAccessDeniedException => exception.Message,
            ReferenceDataValidationException => exception.Message,
            ReferenceDataConflictException => exception.Message,
            _ => "The reference data operation could not be completed."
        };
    }

    protected static string FormatDiscordError(Exception exception)
    {
        return exception switch
        {
            DiscordAuthorizationException => exception.Message,
            DiscordRoleMappingAccessDeniedException => exception.Message,
            DiscordRoleMappingValidationException => exception.Message,
            DiscordRoleMappingConflictException => exception.Message,
            DiscordRoleManagementValidationException => exception.Message,
            DiscordLinkManagementAccessDeniedException => exception.Message,
            DiscordLinkNotFoundException => exception.Message,
            DiscordLinkValidationException => exception.Message,
            DiscordLinkConflictException => exception.Message,
            DiscordRoleGatewayException => exception.Message,
            _ => "The Discord operation could not be completed."
        };
    }
}
