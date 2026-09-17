using NetCord;
using NetCord.Rest;

namespace Felion.Bot.Commands;

internal static class AdministrationCommandResponses
{
    public static InteractionCallbackProperties Success(string message)
    {
        return InteractionCallback.Message(
            new InteractionMessageProperties()
                .WithContent(message)
                .WithFlags(MessageFlags.Ephemeral));
    }

    public static InteractionCallbackProperties Error(string message)
    {
        return InteractionCallback.Message(
            new InteractionMessageProperties()
                .WithContent($"❌ {message}")
                .WithFlags(MessageFlags.Ephemeral));
    }
}
