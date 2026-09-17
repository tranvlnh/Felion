using System.Text.Json;
using Felion.Application.Discord;
using Felion.Application.Members;
using Felion.Domain.Audit;
using Felion.Domain.Common;
using Felion.Domain.Identity;
using Felion.Domain.Members;
using Felion.Domain.Probation;

namespace Felion.Application.Probation;

public sealed class ProbationCandidateManagementService(
    IProbationCandidateStore store,
    IMemberStore memberStore,
    IDiscordRoleSynchronizationService? roleSynchronizationService = null) : IProbationCandidateManagementService
{
    private const int MaxStudentIdLength = 50;
    private const int MaxFullNameLength = 200;
    private const int MaxMentorSearchLength = 200;

    public async Task<ProbationCandidatePage> ListAsync(
        Guid actorMemberId,
        ListProbationCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        ValidateListQuery(query);

        var result = await store.ListAsync(query, cancellationToken);
        return new ProbationCandidatePage(
            result.Items.Select(ToDto).ToArray(),
            query.Page,
            query.PageSize,
            result.TotalCount);
    }

    public async Task<ProbationCandidateDto> GetAsync(
        Guid actorMemberId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        return ToDto(await GetViewAsync(candidateId, cancellationToken));
    }

    public async Task<ProbationCandidateDto> CreateAsync(
        Guid actorMemberId,
        CreateProbationCandidateCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var department = await GetActiveDepartmentAsync(command.DepartmentId, cancellationToken);
        var generation = await GetActiveGenerationAsync(command.GenerationId, cancellationToken);
        var studentId = NormalizeStudentId(command.StudentId);
        ValidateFullName(command.FullName);

        if (await store.IsStudentIdTakenAsync(studentId, excludedCandidateId: null, cancellationToken))
        {
            throw new ProbationCandidateConflictException("StudentId already exists for a Member or probation candidate.");
        }

        ProbationCandidate candidate;
        try
        {
            candidate = ProbationCandidate.Create(studentId, command.FullName, department.Id, generation.Id);
        }
        catch (DomainException exception)
        {
            throw new ProbationCandidateValidationException(exception.Message);
        }

        var audit = CreateAudit(
            actorMemberId,
            "ProbationCandidateCreated",
            candidate.Id,
            correlationId,
            after: Snapshot(candidate));
        await store.AddAsync(candidate, audit, cancellationToken);
        return ToDto(await GetViewAsync(candidate.Id, cancellationToken));
    }

    public async Task<ProbationCandidateDto> UpdateAsync(
        Guid actorMemberId,
        Guid candidateId,
        UpdateProbationCandidateCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var candidate = await store.FindCandidateAsync(candidateId, track: true, cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(candidateId);
        var identityLink = await store.FindIdentityLinkAsync(candidateId, track: true, cancellationToken);
        ValidateIdentityLink(candidate, identityLink);

        var department = await GetActiveDepartmentAsync(command.DepartmentId, cancellationToken);
        var generation = await GetActiveGenerationAsync(command.GenerationId, cancellationToken);
        var studentId = NormalizeStudentId(command.StudentId);
        ValidateFullName(command.FullName);

        if (!string.Equals(studentId, candidate.StudentId, StringComparison.Ordinal)
            && await store.IsStudentIdTakenAsync(studentId, candidate.Id, cancellationToken))
        {
            throw new ProbationCandidateConflictException("StudentId already exists for a Member or probation candidate.");
        }

        var before = Snapshot(candidate);
        try
        {
            candidate.UpdateProfile(studentId, command.FullName, department.Id, generation.Id);
            if (identityLink is not null)
            {
                identityLink.UpdateStudentId(candidate.StudentId);
            }
        }
        catch (DomainException exception)
        {
            throw new ProbationCandidateValidationException(exception.Message);
        }

        var audit = CreateAudit(
            actorMemberId,
            "ProbationCandidateUpdated",
            candidate.Id,
            correlationId,
            before,
            Snapshot(candidate));
        await store.SaveUpdateAsync(candidate, identityLink, audit, cancellationToken);
        if (identityLink is not null && roleSynchronizationService is not null)
        {
            await roleSynchronizationService.SynchronizeSubjectAsync(
                DiscordIdentitySubjectType.Probation,
                candidate.Id,
                cancellationToken);
        }
        return ToDto(await GetViewAsync(candidate.Id, cancellationToken));
    }

    public async Task<ProbationCandidateDto> ChangeTeamAsync(
        Guid actorMemberId,
        Guid candidateId,
        Guid? teamId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var candidate = await store.FindCandidateAsync(candidateId, track: true, cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(candidateId);
        if (candidate.Status != ProbationCandidateStatus.Active)
        {
            throw new ProbationCandidateValidationException("Only an active probation candidate can change team.");
        }

        if (candidate.TeamId == teamId)
        {
            return ToDto(await GetViewAsync(candidateId, cancellationToken));
        }

        if (teamId is not null)
        {
            var team = await store.FindTeamAsync(teamId.Value, track: false, cancellationToken)
                ?? throw new ProbationTeamNotFoundException(teamId.Value);
            if (!team.IsActive)
            {
                throw new ProbationCandidateValidationException("Only an active probation team can be assigned.");
            }
        }

        var before = Snapshot(candidate);
        try
        {
            if (teamId is null)
            {
                candidate.RemoveFromTeam();
            }
            else
            {
                candidate.AssignToTeam(teamId.Value);
            }
        }
        catch (DomainException exception)
        {
            throw new ProbationCandidateValidationException(exception.Message);
        }

        var discordUserId = await FindCandidateDiscordUserIdAsync(candidate.Id, cancellationToken);
        var audit = CreateAudit(
            actorMemberId,
            "ProbationCandidateTeamChanged",
            candidate.Id,
            correlationId,
            before,
            Snapshot(candidate));
        await store.SaveTeamChangeAsync(candidate, audit, cancellationToken);
        if (discordUserId is > 0 && roleSynchronizationService is not null)
        {
            await roleSynchronizationService.SynchronizeSubjectAsync(
                DiscordIdentitySubjectType.Probation,
                candidate.Id,
                cancellationToken);
        }
        return ToDto(await GetViewAsync(candidate.Id, cancellationToken));
    }

    public async Task<ProbationCandidateDiscordSyncResult> ForceSyncDiscordRolesAsync(
        Guid actorMemberId,
        Guid candidateId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var candidate = await store.FindCandidateAsync(candidateId, track: false, cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(candidateId);
        if (candidate.Status != ProbationCandidateStatus.Active)
        {
            throw new ProbationCandidateValidationException(
                "Only an active probation candidate can be synchronized.");
        }

        var link = await store.FindIdentityLinkAsync(candidateId, track: false, cancellationToken)
            ?? throw new ProbationCandidateValidationException(
                "The probation candidate does not have a linked Discord identity.");
        var audit = CreateAudit(
            actorMemberId,
            "ProbationCandidateDiscordRoleSyncRequested",
            candidate.Id,
            correlationId,
            after: JsonSerializer.Serialize(new
            {
                link.DiscordUserId
            }));

        await store.RecordAuditAsync(audit, cancellationToken);

        if (roleSynchronizationService is not null)
        {
            await roleSynchronizationService.SynchronizeSubjectAsync(
                DiscordIdentitySubjectType.Probation,
                candidate.Id,
                cancellationToken);
        }

        return new ProbationCandidateDiscordSyncResult(
            candidate.Id,
            link.DiscordUserId,
            RolesSynchronized: roleSynchronizationService is not null);
    }

    public async Task<ProbationManagementReferenceData> GetReferenceDataAsync(
        Guid actorMemberId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        var departments = await memberStore.ListDepartmentsAsync(cancellationToken);
        var generations = await memberStore.ListGenerationsAsync(cancellationToken);
        var teams = await store.ListTeamsAsync(cancellationToken);

        return new ProbationManagementReferenceData(
            departments
                .Where(department => department.IsActive)
                .OrderBy(department => department.Name)
                .Select(department => new ProbationCandidateReference(department.Id, department.Name, department.Slug))
                .ToArray(),
            generations
                .Where(generation => generation.IsActive)
                .OrderBy(generation => generation.Code)
                .Select(generation => new ProbationCandidateReference(generation.Id, generation.Name, generation.Code))
                .ToArray(),
            teams);
    }

    public async Task<IReadOnlyList<ProbationMentorOption>> SearchMentorsAsync(
        Guid actorMemberId,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedActorAsync(actorMemberId, cancellationToken);
        if (limit is < 1 or > 100)
        {
            throw new ProbationCandidateValidationException("Mentor search limit must be between 1 and 100.");
        }

        if (search?.Trim().Length > MaxMentorSearchLength)
        {
            throw new ProbationCandidateValidationException(
                $"Mentor search must be at most {MaxMentorSearchLength} characters.");
        }

        return await store.SearchActiveMentorsAsync(search, limit, cancellationToken);
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
            throw new ProbationCandidateManagementAccessDeniedException();
        }
    }

    private async Task<Department> GetActiveDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await memberStore.FindDepartmentAsync(departmentId, cancellationToken);
        if (department is null || !department.IsActive)
        {
            throw new ProbationCandidateValidationException("Department must exist and be active.");
        }

        return department;
    }

    private async Task<Generation> GetActiveGenerationAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var generation = await memberStore.FindGenerationAsync(generationId, cancellationToken);
        if (generation is null || !generation.IsActive)
        {
            throw new ProbationCandidateValidationException("Generation must exist and be active.");
        }

        return generation;
    }

    private async Task<ProbationCandidateView> GetViewAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        return await store.FindViewAsync(candidateId, cancellationToken)
            ?? throw new ProbationCandidateNotFoundException(candidateId);
    }

    private async Task<long?> FindCandidateDiscordUserIdAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var link = await store.FindIdentityLinkAsync(candidateId, track: false, cancellationToken);
        return link?.DiscordUserId;
    }

    private static void ValidateListQuery(ListProbationCandidatesQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 200)
        {
            throw new ProbationCandidateValidationException("Page must be positive and PageSize must be between 1 and 200.");
        }

        if (query.Search?.Trim().Length > MaxFullNameLength)
        {
            throw new ProbationCandidateValidationException($"Search must be at most {MaxFullNameLength} characters.");
        }

        if (query.TeamId is not null && query.HasTeam is not null)
        {
            throw new ProbationCandidateValidationException("TeamId and HasTeam cannot be used together.");
        }
    }

    private static string NormalizeStudentId(string studentId)
    {
        try
        {
            var normalized = IdentityNormalizer.StudentId(studentId);
            if (normalized.Length > MaxStudentIdLength)
            {
                throw new ProbationCandidateValidationException(
                    $"StudentId must be at most {MaxStudentIdLength} characters.");
            }

            return normalized;
        }
        catch (DomainException exception)
        {
            throw new ProbationCandidateValidationException(exception.Message);
        }
    }

    private static void ValidateFullName(string fullName)
    {
        try
        {
            var normalized = IdentityNormalizer.RequiredText(fullName, nameof(ProbationCandidate.FullName));
            if (normalized.Length > MaxFullNameLength)
            {
                throw new ProbationCandidateValidationException(
                    $"FullName must be at most {MaxFullNameLength} characters.");
            }
        }
        catch (DomainException exception)
        {
            throw new ProbationCandidateValidationException(exception.Message);
        }
    }

    private static void ValidateIdentityLink(
        ProbationCandidate candidate,
        DiscordIdentityLink? identityLink)
    {
        if (identityLink is not null
            && (identityLink.SubjectType != DiscordIdentitySubjectType.Probation
                || identityLink.SubjectId != candidate.Id
                || identityLink.StudentId != candidate.StudentId))
        {
            throw new ProbationCandidateConflictException(
                "The candidate Discord identity link is inconsistent with the candidate record.");
        }
    }

    private static ProbationCandidateDto ToDto(ProbationCandidateView view)
    {
        var candidate = view.Candidate;
        return new ProbationCandidateDto(
            candidate.Id,
            candidate.StudentId,
            candidate.FullName,
            view.Department,
            view.Generation,
            view.Team,
            candidate.Status,
            view.HasDiscordIdentity,
            candidate.CreatedAt,
            candidate.UpdatedAt);
    }

    private static AuditLog CreateAudit(
        Guid actorMemberId,
        string action,
        Guid candidateId,
        string correlationId,
        string? before = null,
        string? after = null)
    {
        return AuditLog.Create(
            AuditActorType.WebMember,
            actorMemberId,
            actorDiscordUserId: null,
            action,
            "ProbationCandidate",
            candidateId,
            correlationId,
            beforeJson: before,
            afterJson: after);
    }

    private static string Snapshot(ProbationCandidate candidate)
    {
        return JsonSerializer.Serialize(new
        {
            candidate.Id,
            candidate.StudentId,
            candidate.FullName,
            candidate.DepartmentId,
            candidate.GenerationId,
            candidate.TeamId,
            candidate.Status,
            candidate.UpdatedAt
        });
    }
}
