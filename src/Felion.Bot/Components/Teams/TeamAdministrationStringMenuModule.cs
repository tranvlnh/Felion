using Felion.Application.Discord;
using Felion.Application.Probation;
using Felion.Bot.Commands;
using Felion.Bot.Configuration;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace Felion.Bot.Components.Teams;

public sealed class TeamAdministrationStringMenuModule(
    IDiscordAuthorizationService authorizationService,
    IProbationTeamManagementService teamService,
    IProbationCandidateManagementService candidateService,
    IDiscordRoleMappingSubjectResolver subjectResolver,
    ConfiguredDiscordGuild configuredGuild)
    : ComponentInteractionModule<StringMenuInteractionContext>
{
    [ComponentInteraction(TeamInteractionIds.TeamSelect)]
    public async Task<InteractionCallbackProperties> SelectTeamAsync()
    {
        var actor = await FindActorAsync();
        if (actor is null || !Guid.TryParse(Context.SelectedValues.SingleOrDefault(), out var teamId))
        {
            return AdministrationCommandResponses.Error("Team selection is invalid or unauthorized.");
        }

        return await DetailAsync(actor, teamId);
    }

    [ComponentInteraction(TeamInteractionIds.MapKindSelect)]
    public async Task<InteractionCallbackProperties> SelectMappingKindAsync()
    {
        var actor = await FindAdminAsync();
        if (actor is null || !Enum.TryParse(Context.SelectedValues.SingleOrDefault(), out DiscordRoleMappingKind kind))
        {
            return AdministrationCommandResponses.Error("Mapping selection is invalid or unauthorized.");
        }

        try
        {
            var references = await candidateService.GetReferenceDataAsync(
                actor.MemberId,
                CancellationToken.None);
            var subjects = kind switch
            {
                DiscordRoleMappingKind.Position =>
                    Enum.GetValues<MemberPosition>()
                        .Select(position => (position.ToString(), position.ToString()))
                        .ToArray(),
                DiscordRoleMappingKind.Probation => [("Probation", "Probation")],
                DiscordRoleMappingKind.Department => references.Departments
                    .Select(department => (department.Name, department.Name))
                    .ToArray(),
                DiscordRoleMappingKind.Generation => references.Generations
                    .Select(generation => (generation.Name, generation.Name))
                    .ToArray(),
                DiscordRoleMappingKind.ProbationTeam => references.Teams
                    .Select(team => (team.Name, team.Name))
                    .ToArray(),
                _ => []
            };
            return TeamAdministrationComponentSupport.Modify(
                TeamAdministrationMessageFactory.MappingSubjectSelection(kind, subjects));
        }
        catch (Exception exception) when (exception is ProbationCandidateManagementAccessDeniedException
            or ProbationCandidateValidationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.MapSubjectSelect)]
    public async Task<InteractionCallbackProperties> SelectMappingSubjectAsync(DiscordRoleMappingKind kind)
    {
        var actor = await FindAdminAsync();
        var subject = Context.SelectedValues.SingleOrDefault();
        if (actor is null || string.IsNullOrWhiteSpace(subject))
        {
            return AdministrationCommandResponses.Error("Mapping subject is invalid or unauthorized.");
        }

        try
        {
            var subjectKey = await subjectResolver.ResolveAsync(
                kind,
                subject,
                CancellationToken.None);
            return TeamAdministrationComponentSupport.Modify(
                TeamAdministrationMessageFactory.RoleSelection(kind, subjectKey));
        }
        catch (Exception exception) when (exception is DiscordRoleMappingValidationException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.AssignCandidateSelect)]
    public async Task<InteractionCallbackProperties> AssignCandidateAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null || !Guid.TryParse(Context.SelectedValues.SingleOrDefault(), out var candidateId))
        {
            return AdministrationCommandResponses.Error("Candidate selection is invalid or unauthorized.");
        }

        try
        {
            await teamService.AssignCandidateAsync(
                actor.MemberId,
                teamId,
                candidateId,
                TeamAdministrationComponentSupport.CorrelationId(Context.Interaction.Id),
                CancellationToken.None,
                actor.DiscordUserId);
            return await DetailAsync(actor, teamId);
        }
        catch (Exception exception) when (TeamAdministrationComponentSupport.IsTeamException(exception)
            || exception is ProbationCandidateNotFoundException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.RemoveCandidateSelect)]
    public async Task<InteractionCallbackProperties> RemoveCandidateAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null || !Guid.TryParse(Context.SelectedValues.SingleOrDefault(), out var candidateId))
        {
            return AdministrationCommandResponses.Error("Candidate selection is invalid or unauthorized.");
        }

        try
        {
            await teamService.RemoveCandidateAsync(
                actor.MemberId,
                teamId,
                candidateId,
                TeamAdministrationComponentSupport.CorrelationId(Context.Interaction.Id),
                CancellationToken.None,
                actor.DiscordUserId);
            return await DetailAsync(actor, teamId);
        }
        catch (Exception exception) when (TeamAdministrationComponentSupport.IsTeamException(exception)
            || exception is ProbationCandidateNotFoundException)
        {
            return AdministrationCommandResponses.Error(exception.Message);
        }
    }

    [ComponentInteraction(TeamInteractionIds.AssignMentorSelect)]
    public async Task<InteractionCallbackProperties> AssignMentorAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null || !Guid.TryParse(Context.SelectedValues.SingleOrDefault(), out var mentorMemberId))
        {
            return AdministrationCommandResponses.Error("Mentor selection is invalid or unauthorized.");
        }

        try
        {
            await teamService.AssignMentorAsync(
                actor.MemberId,
                teamId,
                mentorMemberId,
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

    [ComponentInteraction(TeamInteractionIds.RemoveMentorSelect)]
    public async Task<InteractionCallbackProperties> RemoveMentorAsync(Guid teamId)
    {
        var actor = await FindActorAsync();
        if (actor is null || !Guid.TryParse(Context.SelectedValues.SingleOrDefault(), out var mentorMemberId))
        {
            return AdministrationCommandResponses.Error("Mentor selection is invalid or unauthorized.");
        }

        try
        {
            await teamService.RemoveMentorAsync(
                actor.MemberId,
                teamId,
                mentorMemberId,
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
