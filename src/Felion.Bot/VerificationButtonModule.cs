using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot;

public sealed class VerificationButtonModule(ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction(VerificationInteractionIds.LinkButton)]
    public InteractionCallbackProperties OpenLinkModal()
    {
        if (!configuredGuild.Matches(Context.Interaction.GuildId))
        {
            return VerificationInteractionResponses.Error(
                "This verification button is only available in the configured Felion guild.");
        }

        var studentIdInput = new TextInputProperties(
                VerificationInteractionIds.StudentIdInput,
                TextInputStyle.Short)
            .WithPlaceholder("e.g. SV001")
            .WithRequired(true);

        return InteractionCallback.Modal(
            new ModalProperties(
                    VerificationInteractionIds.LinkModal,
                    "Link your StudentId")
                .AddComponents(
                [
                    new LabelProperties("StudentId", studentIdInput)
                ]));
    }
}
