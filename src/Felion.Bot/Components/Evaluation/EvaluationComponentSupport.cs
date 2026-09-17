using NetCord.Rest;

namespace Felion.Bot.Components.Evaluation;

internal static class EvaluationComponentSupport
{
    public static InteractionCallbackProperties Modify(InteractionMessageProperties message) =>
        InteractionCallback.ModifyMessage(options =>
            options
                .WithContent(message.Content)
                .WithEmbeds(message.Embeds)
                .WithComponents(message.Components));

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

    public static string CorrelationId(ulong interactionId) =>
        "discord-component-" + interactionId;
}
