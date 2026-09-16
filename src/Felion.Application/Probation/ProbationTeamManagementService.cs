using System.Text.Json;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Probation;

public sealed class ProbationTeamManagementService(
    IProbationTeamStore store,
    IMemberStore memberStore) : IProbationTeamManagementService
{
    public async Task<IReadOnlyList<ProbationTeamDto>> ListAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var teams = await store.ListAsync(cancellationToken);
        return teams.Select(ToDto).ToArray();
    }

    public async Task<ProbationTeamDto> GetAsync(
        Guid actorMemberId,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        return await GetTeamViewAsync(teamId, cancellationToken);
    }

    public async Task<ProbationTeamDto> CreateAsync(
        Guid actorMemberId,
        CreateProbationTeamCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);

        ProbationTeam team;
        try
        {
            team = ProbationTeam.Create(command.Name);
        }
        catch (DomainException exception)
        {
            throw new ProbationTeamValidationException(exception.Message);
        }

        var audit = CreateAudit(
            actorMemberId,
            "ProbationTeamCreated",
            team.Id,
            correlationId,
            after: Snapshot(team));
        await store.AddTeamAsync(team, audit, cancellationToken);
        return ToDto(new ProbationTeamView(
            team.Id,
            team.Name,
            team.IsActive,
            [],
            [],
            team.CreatedAt,
            team.UpdatedAt));
    }

    public async Task<ProbationTeamDto> UpdateAsync(
        Guid actorMemberId,
        Guid teamId,
        UpdateProbationTeamCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        if (command.Name is null && command.IsActive is null)
        {
            throw new ProbationTeamValidationException("At least one team field must be provided.");
        }

        var team = await store.FindTeamAsync(teamId, track: true, cancellationToken)
            ?? throw new ProbationTeamNotFoundException(teamId);
        var before = Snapshot(team);
        try
        {
            if (command.Name is not null)
            {
                team.Rename(command.Name);
            }

            if (command.IsActive is not null)
            {
                team.SetActive(command.IsActive.Value);
            }
        }
        catch (DomainException exception)
        {
            throw new ProbationTeamValidationException(exception.Message);
        }

        var audit = CreateAudit(
            actorMemberId,
            "ProbationTeamUpdated",
            team.Id,
            correlationId,
            before,
            Snapshot(team));
        await store.UpdateTeamAsync(team, audit, cancellationToken);
        return await GetTeamViewAsync(team.Id, cancellationToken);
    }

    public async Task<ProbationTeamDto> AssignCandidateAsync(
        Guid actorMemberId,
        Guid teamId,
        Guid candidateId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var team = await GetActiveTeamAsync(teamId, cancellationToken);
        var candidate = await store.FindCandidateAsync(candidateId, track: true, cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(candidateId);
        if (candidate.Status != ProbationCandidateStatus.Active)
        {
            throw new ProbationTeamValidationException("Only an active probation candidate can be assigned.");
        }

        if (candidate.TeamId == team.Id)
        {
            return await GetTeamViewAsync(team.Id, cancellationToken);
        }

        if (candidate.TeamId is not null)
        {
            throw new ProbationTeamConflictException("A probation candidate can belong to only one team.");
        }

        var before = Snapshot(candidate);
        try
        {
            candidate.AssignToTeam(team.Id);
        }
        catch (DomainException exception)
        {
            throw new ProbationTeamValidationException(exception.Message);
        }

        var syncJob = await CreateCandidateSyncJobAsync(candidate, cancellationToken);
        var audit = CreateAudit(
            actorMemberId,
            "ProbationCandidateAssignedToTeam",
            candidate.Id,
            correlationId,
            before,
            Snapshot(candidate),
            entityType: "ProbationCandidate");
        await store.SaveCandidateAssignmentAsync(candidate, audit, syncJob, cancellationToken);
        return await GetTeamViewAsync(team.Id, cancellationToken);
    }

    public async Task<ProbationTeamDto> RemoveCandidateAsync(
        Guid actorMemberId,
        Guid teamId,
        Guid candidateId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        _ = await GetActiveTeamAsync(teamId, cancellationToken);
        var candidate = await store.FindCandidateAsync(candidateId, track: true, cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(candidateId);
        if (candidate.TeamId != teamId)
        {
            throw new ProbationCandidateNotFoundException(candidateId);
        }

        var before = Snapshot(candidate);
        try
        {
            candidate.RemoveFromTeam();
        }
        catch (DomainException exception)
        {
            throw new ProbationTeamValidationException(exception.Message);
        }

        var syncJob = await CreateCandidateSyncJobAsync(candidate, cancellationToken);
        var audit = CreateAudit(
            actorMemberId,
            "ProbationCandidateRemovedFromTeam",
            candidate.Id,
            correlationId,
            before,
            Snapshot(candidate),
            entityType: "ProbationCandidate");
        await store.SaveCandidateAssignmentAsync(candidate, audit, syncJob, cancellationToken);
        return await GetTeamViewAsync(teamId, cancellationToken);
    }

    public async Task<ProbationTeamDto> AssignMentorAsync(
        Guid actorMemberId,
        Guid teamId,
        Guid mentorMemberId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        _ = await GetActiveTeamAsync(teamId, cancellationToken);
        var mentorMember = await memberStore.FindByIdAsync(mentorMemberId, track: false, cancellationToken);
        if (mentorMember is null || mentorMember.Status != MemberStatus.Active)
        {
            throw new ProbationTeamValidationException("Mentor must be an active Member.");
        }

        var existing = await store.FindMentorAsync(teamId, mentorMemberId, track: true, cancellationToken);
        if (existing is not null)
        {
            return await GetTeamViewAsync(teamId, cancellationToken);
        }

        TeamMentor mentor;
        try
        {
            mentor = TeamMentor.Create(teamId, mentorMemberId);
        }
        catch (DomainException exception)
        {
            throw new ProbationTeamValidationException(exception.Message);
        }

        var audit = CreateAudit(
            actorMemberId,
            "ProbationMentorAssigned",
            teamId,
            correlationId,
            after: JsonSerializer.Serialize(new { mentor.TeamId, mentor.MemberId }));
        await store.AddMentorAsync(mentor, audit, cancellationToken);
        return await GetTeamViewAsync(teamId, cancellationToken);
    }

    public async Task<ProbationTeamDto> RemoveMentorAsync(
        Guid actorMemberId,
        Guid teamId,
        Guid mentorMemberId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        _ = await GetActiveTeamAsync(teamId, cancellationToken);
        var mentor = await store.FindMentorAsync(teamId, mentorMemberId, track: true, cancellationToken)
            ?? throw new ProbationMentorNotFoundException(teamId, mentorMemberId);
        var audit = CreateAudit(
            actorMemberId,
            "ProbationMentorRemoved",
            teamId,
            correlationId,
            before: JsonSerializer.Serialize(new { mentor.TeamId, mentor.MemberId }));
        await store.RemoveMentorAsync(mentor, audit, cancellationToken);
        return await GetTeamViewAsync(teamId, cancellationToken);
    }

    private async Task EnsureAuthorizedActorAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        var actor = await memberStore.FindByIdAsync(actorMemberId, track: false, cancellationToken);
        if (actor is null
            || actor.Status != MemberStatus.Active
            || actor.Position is not (MemberPosition.Admin or MemberPosition.Core))
        {
            throw new ProbationTeamAccessDeniedException();
        }
    }

    private async Task<ProbationTeam> GetActiveTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var team = await store.FindTeamAsync(teamId, track: true, cancellationToken)
            ?? throw new ProbationTeamNotFoundException(teamId);
        if (!team.IsActive)
        {
            throw new ProbationTeamValidationException("Only an active probation team can be changed.");
        }

        return team;
    }

    private async Task<ProbationTeamDto> GetTeamViewAsync(
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var view = await store.FindViewAsync(teamId, cancellationToken)
            ?? throw new ProbationTeamNotFoundException(teamId);
        return ToDto(view);
    }

    private async Task<DiscordSyncJob?> CreateCandidateSyncJobAsync(
        ProbationCandidate candidate,
        CancellationToken cancellationToken)
    {
        var discordUserId = await store.FindCandidateDiscordUserIdAsync(candidate.Id, cancellationToken);
        return discordUserId is > 0
            ? DiscordSyncJob.Create(
                DiscordIdentitySubjectType.Probation,
                candidate.Id,
                DiscordSyncOperation.SynchronizeRoles,
                JsonSerializer.Serialize(new { DiscordUserId = discordUserId.Value }))
            : null;
    }

    private static AuditLog CreateAudit(
        Guid actorMemberId,
        string action,
        Guid entityId,
        string correlationId,
        string? before = null,
        string? after = null,
        string entityType = "ProbationTeam")
    {
        return AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            actorDiscordUserId: null,
            action,
            entityType,
            entityId,
            correlationId,
            beforeJson: before,
            afterJson: after);
    }

    private static ProbationTeamDto ToDto(ProbationTeamView team)
    {
        return new ProbationTeamDto(
            team.Id,
            team.Name,
            team.IsActive,
            team.CandidateIds,
            team.MentorMemberIds,
            team.CreatedAt,
            team.UpdatedAt)
        {
            Candidates = team.Candidates ?? [],
            Mentors = team.Mentors ?? []
        };
    }

    private static string Snapshot(ProbationTeam team)
    {
        return JsonSerializer.Serialize(new
        {
            team.Id,
            team.Name,
            team.IsActive,
            team.CreatedAt,
            team.UpdatedAt
        });
    }

    private static string Snapshot(ProbationCandidate candidate)
    {
        return JsonSerializer.Serialize(new
        {
            candidate.Id,
            candidate.StudentId,
            candidate.TeamId,
            candidate.Status,
            candidate.UpdatedAt
        });
    }
}
