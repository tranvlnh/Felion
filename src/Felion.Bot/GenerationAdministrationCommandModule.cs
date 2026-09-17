using Felion.Application.Discord;
using Felion.Application.Members;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Felion.Bot;

[SlashCommand("generation", "Manage Felion generations")]
public sealed class GenerationAdministrationCommandModule(
    IDiscordAuthorizationService authorizationService,
    IGenerationManagementService generationManagementService,
    ConfiguredDiscordGuild configuredGuild)
    : AdministrationCommandModuleBase(authorizationService, configuredGuild)
{
    [SubSlashCommand("create", "Create a generation")]
    public async Task<InteractionCallbackProperties> CreateAsync(
        [SlashCommandParameter(Description = "The generation display name")] string name,
        [SlashCommandParameter(Description = "The unique generation code")] string code)
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

        try
        {
            var actor = await AuthorizationService.RequireAdminAsync(
                discordUserId,
                CancellationToken.None);
            var generation = await generationManagementService.CreateAsync(
                actor.MemberId,
                new CreateGenerationCommand(name, code),
                CorrelationId(),
                CancellationToken.None,
                actor.DiscordUserId);

            return AdministrationCommandResponses.Success(
                $"✅ Generation **{generation.Name}** (`{generation.Code}`) đã được tạo.");
        }
        catch (Exception exception) when (exception is DiscordAuthorizationException
            or ReferenceDataAccessDeniedException
            or ReferenceDataValidationException
            or ReferenceDataConflictException)
        {
            return AdministrationCommandResponses.Error(
                exception is DiscordAuthorizationException
                    ? exception.Message
                    : FormatReferenceDataError(exception));
        }
    }
}
