using Felion.Application.Discord;
using Felion.Application.Probation;
using Felion.Bot.Commands;
using Felion.Bot.Configuration;
using Felion.Domain.Probation;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot.Components.Teams;

public sealed class TeamAdministrationButtonModule(
    IDiscordAuthorizationService authorizationService,
    IProbationTeamManagementService teamService,
    IProbationCandidateManagementService candidateService,
    ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction(TeamInteractionIds.CreateTeam)]
    public async Task<InteractionCallbackProperties> OpenCreateTeamModalAsync()
    {
        var actor = await FindActorAsync();
        return actor is null
            ? AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.")
            : TeamAdministrationMessageFactory.CreateTeamModal();
    }

    [ComponentInteraction(TeamInteractionIds.Refresh)]
    public async Task<InteractionCallbackProperties> RefreshAsync()
    {
        var actor = await FindActorAsync();
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        return await PanelAsync(actor);
    }

    [ComponentInteraction(TeamInteractionIds.Back)]
    public async Task<InteractionCallbackProperties> BackAsync()
    {
        var actor = await FindActorAsync();
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        return await PanelAsync(actor);
    }

    [ComponentInteraction(TeamInteractionIds.MapRole)]
    public async Task<InteractionCallbackProperties> OpenMappingAsync()
    {
        var actor = await FindAdminAsync();
        return actor is null
            ? AdministrationCommandResponses.Error("Chỉ Admin mới được cấu hình Discord role mapping.")
            : TeamAdministrationComponentSupport.Modify(
                TeamAdministrationMessageFactory.MappingKindSelection());
    }

    [ComponentInteraction(TeamInteractionIds.RenameTeam)]
    public async Task<InteractionCallbackProperties> OpenRenameTeamModalAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        try
        {
            var team = await teamService.GetAsync(actor.MemberId, teamId, CancellationToken.None);
            return TeamAdministrationMessageFactory.RenameTeamModal(team.Id, team.Name);
        }
        catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
            or ProbationTeamNotFoundException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.ToggleTeam)]
    public async Task<InteractionCallbackProperties> ToggleTeamAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        try
        {
            var team = await teamService.GetAsync(actor.MemberId, teamId, CancellationToken.None);
            await teamService.UpdateAsync(
                actor.MemberId,
                teamId,
                new UpdateProbationTeamCommand(IsActive: !team.IsActive),
                TeamAdministrationComponentSupport.CorrelationId(Context.Interaction.Id),
                CancellationToken.None,
                actor.DiscordUserId);
            return await DetailAsync(actor, teamId);
        }
        catch (Exception exception) when (TeamAdministrationComponentSupport.IsTeamException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.AssignCandidate)]
    public async Task<InteractionCallbackProperties> OpenCandidateAssignmentAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        try
        {
            var candidates = await candidateService.ListAsync(
                actor.MemberId,
                new ListProbationCandidatesQuery(
                    PageSize: 25,
                    HasTeam: false,
                    Status: ProbationCandidateStatus.Active),
                CancellationToken.None);
            return TeamAdministrationComponentSupport.Modify(
                TeamAdministrationMessageFactory.CandidateSelection(
                    teamId,
                    candidates.Items,
                    removing: false));
        }
        catch (Exception exception) when (exception is ProbationCandidateManagementAccessDeniedException
            or ProbationCandidateValidationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.RemoveCandidate)]
    public async Task<InteractionCallbackProperties> OpenCandidateRemovalAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        try
        {
            var team = await teamService.GetAsync(actor.MemberId, teamId, CancellationToken.None);
            return TeamAdministrationComponentSupport.Modify(
                TeamAdministrationMessageFactory.CandidateSelection(
                    teamId,
                    team.Candidates
                        .Select(candidate => new ProbationCandidateDto(
                            candidate.Id,
                            candidate.StudentId,
                            candidate.FullName,
                            new ProbationCandidateReference(Guid.Empty, string.Empty),
                            new ProbationCandidateReference(Guid.Empty, string.Empty),
                            null,
                            ProbationCandidateStatus.Active,
                            false,
                            team.UpdatedAt,
                            team.UpdatedAt))
                        .ToArray(),
                    removing: true));
        }
        catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
            or ProbationTeamNotFoundException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.AssignMentor)]
    public async Task<InteractionCallbackProperties> OpenMentorAssignmentAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        try
        {
            var team = await teamService.GetAsync(actor.MemberId, teamId, CancellationToken.None);
            var mentors = (await candidateService.SearchMentorsAsync(
                    actor.MemberId,
                    search: null,
                    limit: 25,
                    CancellationToken.None))
                .Where(mentor => !team.MentorMemberIds.Contains(mentor.MemberId))
                .ToArray();
            return TeamAdministrationComponentSupport.Modify(
                TeamAdministrationMessageFactory.MentorSelection(teamId, mentors, removing: false));
        }
        catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
            or ProbationTeamNotFoundException
            or ProbationCandidateManagementAccessDeniedException
            or ProbationCandidateValidationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.RemoveMentor)]
    public async Task<InteractionCallbackProperties> OpenMentorRemovalAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null)
        {
            return AdministrationCommandResponses.Error("Bạn không có quyền quản lý probation team.");
        }

        try
        {
            var team = await teamService.GetAsync(actor.MemberId, teamId, CancellationToken.None);
            return TeamAdministrationComponentSupport.Modify(
                TeamAdministrationMessageFactory.MentorSelection(
                    teamId,
                    team.Mentors.Select(mentor => new ProbationMentorOption(
                        mentor.MemberId,
                        mentor.StudentId,
                        mentor.FullName)).ToArray(),
                    removing: true));
        }
        catch (Exception exception) when (exception is ProbationTeamAccessDeniedException
            or ProbationTeamNotFoundException)
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

    private async Task<DiscordAdminActor?> FindAdminAsync()
    {
        if (!configuredGuild.Matches(Context.Interaction.GuildId)
            || !TeamAdministrationComponentSupport.TryGetDiscordUserId(
                Context.Interaction.User.Id,
                out var discordUserId))
        {
            return null;
        }

        return await authorizationService.FindAdminAsync(discordUserId, CancellationToken.None);
    }

    private async Task<InteractionCallbackProperties> PanelAsync(DiscordManagementActor actor)
    {
        var message = TeamAdministrationMessageFactory.Panel(
            await teamService.ListAsync(actor.MemberId, CancellationToken.None));
        return TeamAdministrationComponentSupport.Modify(message);
    }

    private async Task<InteractionCallbackProperties> DetailAsync(
        DiscordManagementActor actor,
        Guid teamId)
    {
        try
        {
            var message = TeamAdministrationMessageFactory.Detail(
                await teamService.GetAsync(actor.MemberId, teamId, CancellationToken.None));
            return TeamAdministrationComponentSupport.Modify(message);
        }
        catch (Exception exception) when (TeamAdministrationComponentSupport.IsTeamException(exception))
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }
}
