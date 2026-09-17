using Felion.Application.Discord;
using Felion.Application.Probation;
using NetCord.Rest;

namespace Felion.Bot.Components.Teams;

internal static class TeamAdministrationComponentSupport
{
    public static InteractionCallbackProperties Modify(InteractionMessageProperties message)
    {
        return InteractionCallback.ModifyMessage(options =>
            options
                .WithContent(message.Content)
                .WithEmbeds(message.Embeds)
                .WithComponents(message.Components));
    }

    public static bool TryGetDiscordUserId(ulong rawUserId, out long discordUserId)
    {
        if (rawUserId == 0 || rawUserId > long.MaxValue)
        {
            discordUserId = 0;
            return false;
        }

        discordUserId = (long)rawUserId;
        return true;
    }

    public static string CorrelationId(ulong interactionId)
    {
        return $"discord-component-{interactionId}";
    }

    public static bool IsTeamException(Exception exception)
    {
        return exception is ProbationTeamAccessDeniedException
            or ProbationTeamNotFoundException
            or ProbationMentorNotFoundException
            or ProbationTeamValidationException
            or ProbationTeamConflictException
            or DiscordRoleGatewayException;
    }
}
