using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Bot.Configuration;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Felion.Bot.Commands;

[SlashCommand("department", "Manage Felion departments")]
public sealed class DepartmentAdministrationCommandModule(
    IDiscordAuthorizationService authorizationService,
    IDepartmentManagementService departmentManagementService,
    ConfiguredDiscordGuild configuredGuild)
    : AdministrationCommandModuleBase(authorizationService, configuredGuild)
{
    [SubSlashCommand("create", "Create a department")]
    public async Task<InteractionCallbackProperties> CreateAsync(
        [SlashCommandParameter(Description = "The department display name")] string name,
        [SlashCommandParameter(Description = "The unique lowercase department slug")] string slug)
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
            var department = await departmentManagementService.CreateAsync(
                actor.MemberId,
                new CreateDepartmentCommand(name, slug),
                CorrelationId(),
                CancellationToken.None,
                actor.DiscordUserId);

            return AdministrationCommandResponses.Success(
                $"✅ Department **{department.Name}** (`{department.Slug}`) đã được tạo.");
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
