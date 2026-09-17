using Felion.Application.Discord;
using Felion.Application.Probation;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Felion.Bot;

[SlashCommand("team", "Manage Felion probation teams")]
public sealed class TeamAdministrationCommandModule(
    IDiscordAuthorizationService authorizationService,
    IProbationTeamManagementService teamService,
    ConfiguredDiscordGuild configuredGuild)
    : AdministrationCommandModuleBase(authorizationService, configuredGuild)
{
    [SubSlashCommand("panel", "Open the interactive probation team panel")]
    public async Task<InteractionCallbackProperties> PanelAsync()
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
            var actor = await AuthorizationService.RequireCoreOrAdminAsync(
                discordUserId,
                CancellationToken.None);
            var teams = await teamService.ListAsync(actor.MemberId, CancellationToken.None);
            return InteractionCallback.Message(
                TeamAdministrationMessageFactory.Panel(
                    teams,
                    actor.Position == Domain.Members.MemberPosition.Admin));
        }
        catch (Exception exception) when (exception is DiscordAuthorizationException
            or ProbationTeamAccessDeniedException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [SubSlashCommand("create", "Create a probation team")]
    public async Task<InteractionCallbackProperties> CreateAsync(
        [SlashCommandParameter(Description = "The team display name")] string name)
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
            var actor = await AuthorizationService.RequireCoreOrAdminAsync(
                discordUserId,
                CancellationToken.None);
            var team = await teamService.CreateAsync(
                actor.MemberId,
                new CreateProbationTeamCommand(name),
                CorrelationId(),
                CancellationToken.None,
                actor.DiscordUserId);
            return AdministrationCommandResponses.Success(
                $"✅ Team **{team.Name}** đã được tạo. Dùng `/team panel` để tiếp tục cấu hình.");
        }
        catch (Exception exception) when (exception is DiscordAuthorizationException
            or ProbationTeamAccessDeniedException
            or ProbationTeamValidationException
            or ProbationTeamConflictException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }
}
