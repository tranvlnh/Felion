using Felion.Application.Discord;
using Felion.Application.Probation;
using Felion.Bot.Commands;
using Felion.Bot.Configuration;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot.Components.Teams;

public sealed class TeamAdministrationModalModule(
    IDiscordAuthorizationService authorizationService,
    IProbationTeamManagementService teamService,
    ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction(TeamInteractionIds.CreateTeamModal)]
    public async Task<InteractionCallbackProperties> CreateTeamAsync()
    {
        var actor = await FindActorAsync();
        var name = GetTextInput("team-name");
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return AdministrationCommandResponses.Error("Tên team là bắt buộc.");
        }

        try
        {
            var team = await teamService.CreateAsync(
                actor.MemberId,
                new CreateProbationTeamCommand(name),
                TeamAdministrationComponentSupport.CorrelationId(Context.Interaction.Id),
                CancellationToken.None,
                actor.DiscordUserId);
            return AdministrationCommandResponses.Success($"✅ Team **{team.Name}** đã được tạo.");
        }
        catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
            or ProbationTeamValidationException
            or ProbationTeamConflictException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.RenameTeamModal)]
    public async Task<InteractionCallbackProperties> RenameTeamAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        var name = GetTextInput("team-name");
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return AdministrationCommandResponses.Error("Tên team là bắt buộc.");
        }

        try
        {
            var team = await teamService.UpdateAsync(
                actor.MemberId,
                teamId,
                new UpdateProbationTeamCommand(Name: name),
                TeamAdministrationComponentSupport.CorrelationId(Context.Interaction.Id),
                CancellationToken.None,
                actor.DiscordUserId);
            return AdministrationCommandResponses.Success($"✅ Team đã đổi tên thành **{team.Name}**.");
        }
        catch (Exception exception) when (TeamAdministrationComponentSupport.IsTeamException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    private async Task<DiscordManagementActor?> FindActorAsync()
    {
        if (!configuredGuild.Matches(Context.Interaction.GuildId)
            || !TeamAdministrationComponentSupport.TryGetDiscordUserId(
                Context.Interaction.User.Id,
                out var discordUserId))
        {
            return null;
        }

        return await authorizationService.FindCoreOrAdminAsync(
            discordUserId,
            CancellationToken.None);
    }

    private string? GetTextInput(string customId)
    {
        return Context.Components
            .OfType<Label>()
            .Select(label => label.Component)
            .OfType<TextInput>()
            .SingleOrDefault(input => input.CustomId == customId)
            ?.Value;
    }
}
