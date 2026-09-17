using NetCord;
using NetCord.Rest;

namespace Felion.Bot.Components.Verification;

internal static class VerificationInteractionResponses
{
    public static InteractionCallbackProperties Error(string message)
    {
        return InteractionCallback.Message(
            new InteractionMessageProperties()
                .WithContent(message)
                .WithFlags(MessageFlags.Ephemeral));
    }
}
