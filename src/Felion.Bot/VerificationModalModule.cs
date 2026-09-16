using Felion.Application.Discord;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot;

public sealed class VerificationModalModule(
    IDiscordLinkingService linkingService,
    ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction(VerificationInteractionIds.LinkModal)]
    public async Task<InteractionCallbackProperties> LinkAsync()
    {
        if (!configuredGuild.Matches(Context.Interaction.GuildId))
        {
            return VerificationInteractionResponses.Error(
                "This verification form is only available in the configured Felion guild.");
        }

        if (Context.Interaction.User.Id > long.MaxValue)
        {
            return VerificationInteractionResponses.Error(
                "The Discord user ID is outside the supported range.");
        }

        var studentId = Context.Components
            .OfType<Label>()
            .Select(label => label.Component)
            .OfType<TextInput>()
            .SingleOrDefault(input => input.CustomId == VerificationInteractionIds.StudentIdInput)
            ?.Value;

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return VerificationInteractionResponses.Error("StudentId is required.");
        }

        try
        {
            var result = await linkingService.LinkAsync(
                (long)Context.Interaction.User.Id,
                studentId,
                Context.Interaction.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CancellationToken.None);

            var syncMessage = result.SyncQueued
                ? " Discord role synchronization has been queued."
                : string.Empty;

            return InteractionCallback.Message(
                new InteractionMessageProperties()
                    .WithContent($"StudentId {result.StudentId} is linked successfully.{syncMessage}")
                    .WithFlags(MessageFlags.Ephemeral));
        }
        catch (DiscordLinkValidationException exception)
        {
            return VerificationInteractionResponses.Error(exception.Message);
        }
        catch (DiscordLinkSubjectNotFoundException)
        {
            return VerificationInteractionResponses.Error(
                "No active Member or ProbationCandidate matches that StudentId.");
        }
        catch (DiscordLinkSubjectAmbiguousException)
        {
            return VerificationInteractionResponses.Error(
                "That StudentId matches more than one active identity. Contact an administrator.");
        }
        catch (DiscordLinkConflictException exception)
        {
            return VerificationInteractionResponses.Error(exception.Message);
        }
    }
}
